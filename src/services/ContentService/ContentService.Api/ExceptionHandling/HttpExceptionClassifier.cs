using System.Text.Json;
using ContentService.Api.Common;
using ContentService.Domain.Exceptions;

namespace ContentService.Api.ExceptionHandling;

/// <summary>
/// Classifies domain and HTTP transport exceptions into semantic HTTP status codes, titles, and client-safe detail messages.
/// </summary>
internal static class HttpExceptionClassifier
{
        private const string VALIDATION_ERROR_TITLE = "Validation failed.";
        private const string NOT_FOUND_TITLE = "Resource not found.";
        private const string ENTITY_IN_USE_TITLE = "Resource is in use.";
        private const string CONCURRENCY_CONFLICT_TITLE = "Concurrency conflict.";
        private const string QUOTA_EXCEEDED_TITLE = "Storage quota exceeded.";
        private const string BAD_REQUEST_TITLE = "Malformed request.";
        private const string UNSUPPORTED_MEDIA_TYPE_TITLE = "Unsupported media type.";
        private const string PAYLOAD_TOO_LARGE_TITLE = "Payload too large.";
        private const string BAD_GATEWAY_TITLE = "Upstream storage node request failed.";
        private const string INTERNAL_ERROR_TITLE = "An unexpected error occurred.";
        private const string GENERIC_INTERNAL_DETAIL = "An unexpected server error occurred while processing the request.";

        /// <summary>
        /// Translates domain and transport exceptions into semantic HTTP status codes while keeping internal 500 diagnostics redacted outside Development.
        /// </summary>
        /// <param name="exception">The thrown exception.</param>
        /// <param name="isDevelopment">Whether the host is running in Development.</param>
        /// <returns>A tuple containing the HTTP status code, title, and client-safe detail message.</returns>
        public static (int StatusCode, string Title, string Detail) Classify(Exception exception, bool isDevelopment) =>
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
                        HttpRequestException httpException => (
                                StatusCodes.Status502BadGateway,
                                BAD_GATEWAY_TITLE,
                                isDevelopment ? httpException.Message : BAD_GATEWAY_TITLE),
                        BadHttpRequestException badHttpException => ClassifyBadHttpRequest(badHttpException: badHttpException),
                        InvalidDataException invalidDataException => ClassifyInvalidData(invalidDataException: invalidDataException),
                        _ => (
                                StatusCodes.Status500InternalServerError,
                                INTERNAL_ERROR_TITLE,
                                isDevelopment ? exception.Message : GENERIC_INTERNAL_DETAIL)
                };

        /// <summary>
        /// Unwraps Minimal API <see cref="BadHttpRequestException"/> wrappers so transport status codes (413, 415) and oversized multipart errors are not misreported as generic 400s.
        /// </summary>
        private static (int StatusCode, string Title, string Detail) ClassifyBadHttpRequest(BadHttpRequestException badHttpException)
        {
                // Minimal API form binding wraps FormOptions InvalidDataException inside a 400 BadHttpRequestException, which would otherwise mask 413 Payload Too Large.
                if (badHttpException.InnerException is InvalidDataException innerInvalidData)
                {
                        return ClassifyInvalidData(invalidDataException: innerInvalidData);
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
        private static (int StatusCode, string Title, string Detail) ClassifyInvalidData(InvalidDataException invalidDataException)
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

