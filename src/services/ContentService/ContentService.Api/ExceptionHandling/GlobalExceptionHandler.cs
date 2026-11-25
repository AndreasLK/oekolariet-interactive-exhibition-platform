using System.Text.Json;
using ContentService.Api.Common;
using ContentService.Domain.Exceptions;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace ContentService.Api.ExceptionHandling;

/// <summary>
/// Centralizes HTTP error translation so domain, concurrency, quota, and binding faults return consistent RFC 7807 <see cref="ProblemDetails"/> payloads without leaking stack traces or stripping CORS headers.
/// </summary>
internal sealed class GlobalExceptionHandler : IExceptionHandler
{
        /// <summary>
        /// Consistent client-facing title signaling invalid input or broken domain invariants.
        /// </summary>
        private const string VALIDATION_ERROR_TITLE = "Validation failed.";

        /// <summary>
        /// Consistent client-facing title signaling a missing or soft-deleted resource.
        /// </summary>
        private const string NOT_FOUND_TITLE = "Resource not found.";

        /// <summary>
        /// Consistent client-facing title signaling deletion was blocked to protect active template references.
        /// </summary>
        private const string ENTITY_IN_USE_TITLE = "Resource is in use.";

        /// <summary>
        /// Consistent client-facing title signaling a stale <c>ExpectedConcurrencyStamp</c> so the UI knows to reload before retrying.
        /// </summary>
        private const string CONCURRENCY_CONFLICT_TITLE = "Concurrency conflict.";

        /// <summary>
        /// Consistent client-facing title signaling node storage capacity has been exhausted.
        /// </summary>
        private const string QUOTA_EXCEEDED_TITLE = "Storage quota exceeded.";

        /// <summary>
        /// Consistent client-facing title signaling syntax-level JSON or multipart framing errors.
        /// </summary>
        private const string BAD_REQUEST_TITLE = "Malformed request.";

        /// <summary>
        /// Consistent client-facing title signaling an unsupported HTTP <c>Content-Type</c> header.
        /// </summary>
        private const string UNSUPPORTED_MEDIA_TYPE_TITLE = "Unsupported media type.";

        /// <summary>
        /// Consistent client-facing title signaling an upload exceeded server transport size limits.
        /// </summary>
        private const string PAYLOAD_TOO_LARGE_TITLE = "Payload too large.";

        /// <summary>
        /// Generic title concealing internal implementation details on unhandled 500 faults.
        /// </summary>
        private const string INTERNAL_ERROR_TITLE = "An unexpected error occurred.";

        /// <summary>
        /// Redacted detail returned outside Development so database or stack internals are never exposed to clients.
        /// </summary>
        private const string GENERIC_INTERNAL_DETAIL = "An unexpected server error occurred while processing the request.";

        /// <summary>
        /// Used to serialize standardized RFC 7807 payloads instead of hand-rolling JSON error bodies.
        /// </summary>
        private readonly IProblemDetailsService _problemDetailsService;

        /// <summary>
        /// Re-evaluates CORS headers after ASP.NET Core's exception middleware clears response headers.
        /// </summary>
        private readonly ICorsService _corsService;

        /// <summary>
        /// Resolves the same named Blazor CORS policy used by successful endpoints so error responses remain readable in browsers.
        /// </summary>
        private readonly ICorsPolicyProvider _corsPolicyProvider;

        /// <summary>
        /// Gates whether raw exception messages are shown on 500 responses (Development only) or redacted.
        /// </summary>
        private readonly IHostEnvironment _environment;

        /// <summary>
        /// Records unexpected 5xx server faults while avoiding log noise for expected 4xx client errors.
        /// </summary>
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

                (int statusCode, string title, string detail) = this.MapException(exception: exception);

                // Log 5xx faults, but exclude 507 Insufficient Storage because full quota is an operational capacity condition rather than an application bug.
                if (statusCode >= StatusCodes.Status500InternalServerError && statusCode != StatusCodes.Status507InsufficientStorage)
                {
                        this._logger.LogError(
                                exception: exception,
                                message: "Unhandled exception while processing {Method} {Path}.",
                                args: [httpContext.Request.Method, httpContext.Request.Path]);
                }

                // Re-apply CORS headers because ExceptionHandlerMiddleware wipes all response headers before invoking IExceptionHandler, which otherwise blinds Blazor WASM to 4xx/5xx ProblemDetails.
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
        /// <param name="httpContext">The active HTTP context.</param>
        /// <returns>A task representing the asynchronous CORS header restoration.</returns>
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

        /// <summary>
        /// Translates domain and transport exceptions into semantic HTTP status codes while keeping internal 500 diagnostics redacted outside Development.
        /// </summary>
        /// <param name="exception">The thrown exception.</param>
        /// <returns>A tuple containing the HTTP status code, title, and client-safe detail message.</returns>
        private (int StatusCode, string Title, string Detail) MapException(Exception exception) =>
                // Match specific DomainException subclasses before the base DomainException fallback so 409/507 semantics are not collapsed into 400.
                exception switch
                {
                        DomainValidationException => (StatusCodes.Status400BadRequest, VALIDATION_ERROR_TITLE, exception.Message),
                        KeyNotFoundException => (StatusCodes.Status404NotFound, NOT_FOUND_TITLE, exception.Message),
                        EntityInUseException => (StatusCodes.Status409Conflict, ENTITY_IN_USE_TITLE, exception.Message),
                        ConcurrencyConflictException => (StatusCodes.Status409Conflict, CONCURRENCY_CONFLICT_TITLE, exception.Message),
                        StorageQuotaExceededException => (StatusCodes.Status507InsufficientStorage, QUOTA_EXCEEDED_TITLE, exception.Message),
                        DomainException => (StatusCodes.Status400BadRequest, VALIDATION_ERROR_TITLE, exception.Message),
                        JsonException => (StatusCodes.Status400BadRequest, BAD_REQUEST_TITLE, exception.Message),
                        BadHttpRequestException badHttpException => MapBadHttpRequest(badHttpException: badHttpException),
                        InvalidDataException invalidDataException => MapInvalidData(invalidDataException: invalidDataException),
                        _ => (
                                StatusCodes.Status500InternalServerError,
                                INTERNAL_ERROR_TITLE,
                                this._environment.IsDevelopment() ? exception.Message : GENERIC_INTERNAL_DETAIL)
                };

        /// <summary>
        /// Unwraps Minimal API <see cref="BadHttpRequestException"/> wrappers so transport status codes (413, 415) and oversized multipart errors are not misreported as generic 400s.
        /// </summary>
        /// <param name="badHttpException">The transport binding exception.</param>
        /// <returns>A tuple containing the preserved status code, title, and message.</returns>
        private static (int StatusCode, string Title, string Detail) MapBadHttpRequest(BadHttpRequestException badHttpException)
        {
                // Minimal API form binding wraps FormOptions InvalidDataException inside a 400 BadHttpRequestException, which would otherwise mask 413 Payload Too Large.
                if (badHttpException.InnerException is InvalidDataException innerInvalidData)
                {
                        return MapInvalidData(invalidDataException: innerInvalidData);
                }

                string title = badHttpException.StatusCode switch
                {
                        StatusCodes.Status413PayloadTooLarge => PAYLOAD_TOO_LARGE_TITLE,
                        StatusCodes.Status415UnsupportedMediaType => UNSUPPORTED_MEDIA_TYPE_TITLE,
                        _ => BAD_REQUEST_TITLE
                };

                return (badHttpException.StatusCode, title, badHttpException.Message);
        }

        /// <summary>
        /// Distinguishes multipart body length-limit overflows (HTTP 413) from malformed multipart syntax (HTTP 400) so clients get accurate retry/resize feedback.
        /// </summary>
        /// <param name="invalidDataException">The multipart parsing exception.</param>
        /// <returns>A tuple containing 413 or 400, title, and message.</returns>
        private static (int StatusCode, string Title, string Detail) MapInvalidData(InvalidDataException invalidDataException)
        {
                // ASP.NET Core surfaces MultipartBodyLengthLimit breaches as InvalidDataException without a distinct subclass, requiring a message-fragment check.
                bool isLengthExceeded = invalidDataException.Message.Contains(
                        value: ApiConstants.MULTIPART_LENGTH_LIMIT_MESSAGE_FRAGMENT,
                        comparisonType: StringComparison.OrdinalIgnoreCase);

                return isLengthExceeded
                        ? (StatusCodes.Status413PayloadTooLarge, PAYLOAD_TOO_LARGE_TITLE, invalidDataException.Message)
                        : (StatusCodes.Status400BadRequest, BAD_REQUEST_TITLE, invalidDataException.Message);
        }
}
