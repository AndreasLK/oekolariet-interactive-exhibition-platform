using ContentService.Api.Common;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace ContentService.Api.ExceptionHandling;

/// <summary>
/// Centralizes HTTP error translation so domain, concurrency, quota, and binding faults return consistent RFC 7807 <see cref="ProblemDetails"/> payloads without leaking stack traces or stripping CORS headers.
/// </summary>
/// <remarks>
/// Non-standard error middleware behavior:
/// <list type="bullet">
/// <item>Explicitly re-applies CORS headers before writing <see cref="ProblemDetails"/> because ASP.NET Core's <c>ExceptionHandlerMiddleware</c> clears response headers before invoking <see cref="IExceptionHandler"/> (which would otherwise blind Blazor WASM to 4xx/5xx error details).</item>
/// <item>Distinguishes <see cref="OperationCanceledException"/> with <c>RequestAborted</c> (499 client disconnect) from <c>HttpClient</c> timeouts (504 Gateway Timeout).</item>
/// </list>
/// </remarks>
internal sealed class GlobalExceptionHandler : IExceptionHandler
{
        private const string GATEWAY_TIMEOUT_TITLE = "Upstream storage node timed out.";

        private readonly IProblemDetailsService _problemDetailsService;
        private readonly ICorsService _corsService;
        private readonly ICorsPolicyProvider _corsPolicyProvider;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<GlobalExceptionHandler> _logger;

        /// <summary>
        /// Captures services needed to format safe RFC 7807 error responses and preserve CORS headers across exceptions.
        /// </summary>
        /// <param name="problemDetailsService">The RFC 7807 ProblemDetails writer.</param>
        /// <param name="corsService">The CORS evaluation service.</param>
        /// <param name="corsPolicyProvider">The CORS policy provider.</param>
        /// <param name="environment">The hosting environment.</param>
        /// <param name="logger">The category logger.</param>
        public GlobalExceptionHandler(
                IProblemDetailsService problemDetailsService,
                ICorsService corsService,
                ICorsPolicyProvider corsPolicyProvider,
                IHostEnvironment environment,
                ILogger<GlobalExceptionHandler> logger)
        {
                ArgumentNullException.ThrowIfNull(problemDetailsService);
                ArgumentNullException.ThrowIfNull(corsService);
                ArgumentNullException.ThrowIfNull(corsPolicyProvider);
                ArgumentNullException.ThrowIfNull(environment);
                ArgumentNullException.ThrowIfNull(logger);

                this._problemDetailsService = problemDetailsService;
                this._corsService = corsService;
                this._corsPolicyProvider = corsPolicyProvider;
                this._environment = environment;
                this._logger = logger;
        }

        /// <inheritdoc />
        public async ValueTask<bool> TryHandleAsync(
                HttpContext httpContext,
                Exception exception,
                CancellationToken cancellationToken)
        {
                ArgumentNullException.ThrowIfNull(httpContext);
                ArgumentNullException.ThrowIfNull(exception);

                // Yield to the server if response bytes already started streaming, because mutating status codes or headers mid-body corrupts the HTTP wire format.
                if (httpContext.Response.HasStarted)
                {
                        return false;
                }

                // Treat client-aborted transfers as expected 499 disconnects so cancelled image streams do not trigger 500 error responses or fault alerts.
                if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
                {
                        httpContext.Response.StatusCode = ApiConstants.CLIENT_CLOSED_REQUEST_STATUS_CODE;
                        return true;
                }

                // WHY: HttpClient timeouts throw TaskCanceledException (an OperationCanceledException subclass) even when the client did not disconnect;
                // map those to 504 Gateway Timeout instead of 499 or 500.
                if (exception is OperationCanceledException)
                {
                        return await this.WriteProblemDetailsAsync(
                                httpContext: httpContext,
                                exception: exception,
                                statusCode: StatusCodes.Status504GatewayTimeout,
                                title: GATEWAY_TIMEOUT_TITLE,
                                detail: "The upstream storage node did not respond within the allowed timeout.");
                }

                (int statusCode, string title, string detail) = HttpExceptionClassifier.Classify(
                        exception: exception,
                        isDevelopment: this._environment.IsDevelopment());

                // Log 5xx faults, but exclude 507 Insufficient Storage because full quota is an operational capacity condition rather than an application bug.
                if (statusCode >= StatusCodes.Status500InternalServerError && statusCode != StatusCodes.Status507InsufficientStorage)
                {
                        this._logger.LogError(
                                exception: exception,
                                message: "Unhandled exception while processing {Method} {Path}.",
                                args: [httpContext.Request.Method, httpContext.Request.Path]);
                }

                // Re-apply CORS headers because ExceptionHandlerMiddleware wipes all response headers before invoking IExceptionHandler, which otherwise blinds Blazor WASM to 4xx/5xx ProblemDetails.
                return await this.WriteProblemDetailsAsync(
                        httpContext: httpContext,
                        exception: exception,
                        statusCode: statusCode,
                        title: title,
                        detail: detail);
        }

        /// <summary>
        /// Restores CORS headers and writes a standardized RFC 7807 <see cref="ProblemDetails"/> response.
        /// </summary>
        private async ValueTask<bool> WriteProblemDetailsAsync(
                HttpContext httpContext,
                Exception exception,
                int statusCode,
                string title,
                string detail)
        {
                await this.ApplyCorsHeadersIfCrossOriginAsync(httpContext: httpContext);

                httpContext.Response.StatusCode = statusCode;
                ProblemDetails problemDetails = new()
                {
                        Status = statusCode,
                        Title = title,
                        Detail = detail,
                        Instance = httpContext.Request.Path
                };

                return await this._problemDetailsService.TryWriteAsync(new ProblemDetailsContext
                {
                        HttpContext = httpContext,
                        Exception = exception,
                        ProblemDetails = problemDetails
                });
        }

        /// <summary>
        /// Restores Blazor CORS headers on error responses when an <c>Origin</c> header is present so browser clients can inspect ProblemDetails instead of hitting opaque CORS blocks.
        /// </summary>
        private async Task ApplyCorsHeadersIfCrossOriginAsync(HttpContext httpContext)
        {
                // Skip CORS evaluation for same-origin requests so unnecessary policy lookups are avoided.
                if (!httpContext.Request.Headers.ContainsKey(HeaderNames.Origin))
                {
                        return;
                }

                // Exit quietly if the named CORS policy is absent (e.g., in partial test hosts) so error handling never fails with a secondary exception.
                CorsPolicy? policy = await this._corsPolicyProvider.GetPolicyAsync(
                        context: httpContext,
                        policyName: ApiConstants.BLAZOR_CORS_POLICY_NAME);
                if (policy is null)
                {
                        return;
                }

                CorsResult corsResult = this._corsService.EvaluatePolicy(context: httpContext, policy: policy);
                this._corsService.ApplyResult(result: corsResult, response: httpContext.Response);
        }
}
