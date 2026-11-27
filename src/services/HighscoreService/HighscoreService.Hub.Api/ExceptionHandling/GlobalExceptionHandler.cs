using HighscoreService.Domain.Exceptions;
using HighscoreService.Hub.Api.Extensions;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace HighscoreService.Hub.Api.ExceptionHandling
{
        /// <summary>
        /// Converts domain and unhandled exceptions into RFC 7807 <see cref="ProblemDetails"/> responses.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        /// <param name="corsService">CORS service used to re-apply CORS headers after exception response clearing.</param>
        /// <param name="corsPolicyProvider">CORS policy provider.</param>
        public sealed class GlobalExceptionHandler(
                ILogger<GlobalExceptionHandler> logger,
                ICorsService corsService,
                ICorsPolicyProvider corsPolicyProvider) : IExceptionHandler
        {
                private const int CLIENT_CLOSED_REQUEST_STATUS_CODE = 499;

                private readonly ILogger<GlobalExceptionHandler> _logger = logger;
                private readonly ICorsService _corsService = corsService;
                private readonly ICorsPolicyProvider _corsPolicyProvider = corsPolicyProvider;

                /// <inheritdoc />
                public async ValueTask<bool> TryHandleAsync(
                        HttpContext httpContext,
                        Exception exception,
                        CancellationToken cancellationToken)
                {
                        // WHY: Treat caller-aborted requests as 499 Client Closed Request instead of
                        // logging false-positive 500 errors when a kiosk or browser cancels mid-flight.
                        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
                        {
                                httpContext.Response.StatusCode = CLIENT_CLOSED_REQUEST_STATUS_CODE;
                                return true;
                        }

                        (int statusCode, string title) = exception switch
                        {
                                DomainValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
                                BadHttpRequestException => (StatusCodes.Status400BadRequest, "Malformed request"),
                                EntityNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
                                _ => (StatusCodes.Status500InternalServerError, "Internal server error")
                        };

                        if (statusCode == StatusCodes.Status500InternalServerError)
                        {
                                this._logger.LogError(exception, "Unhandled exception while processing request.");
                        }

                        // ExceptionHandlerMiddleware calls Response.Clear() before invoking IExceptionHandler,
                        // which wipes any Access-Control-Allow-Origin headers written by CorsMiddleware.
                        // Re-apply the Hub CORS policy here so browser/WASM clients receive ProblemDetails
                        // instead of an opaque CORS network failure.
                        await this.ApplyCorsHeadersIfConfiguredAsync(httpContext);

                        ProblemDetails details = new()
                        {
                                Status = statusCode,
                                Title = title,
                                Detail = statusCode == StatusCodes.Status500InternalServerError
                                        ? "An unexpected error occurred."
                                        : exception.Message,
                                Instance = $"{httpContext.Request.PathBase}{httpContext.Request.Path}"
                        };

                        httpContext.Response.StatusCode = statusCode;
                        await httpContext.Response.WriteAsJsonAsync(details, cancellationToken);
                        return true;
                }

                /// <summary>
                /// Re-evaluates and writes CORS headers onto the error response when a Hub CORS policy is registered.
                /// </summary>
                /// <param name="httpContext">Current HTTP context.</param>
                private async Task ApplyCorsHeadersIfConfiguredAsync(HttpContext httpContext)
                {
                        CorsPolicy? policy = await this._corsPolicyProvider.GetPolicyAsync(
                                context: httpContext,
                                policyName: ReverseProxySetup.CORS_POLICY_NAME);

                        if (policy is null)
                        {
                                return;
                        }

                        CorsResult result = this._corsService.EvaluatePolicy(httpContext, policy);
                        this._corsService.ApplyResult(result, httpContext.Response);
                }
        }
}

