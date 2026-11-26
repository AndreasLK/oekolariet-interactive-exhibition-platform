using System.Text;
using System.Text.Json;
using ContentService.Sdk.Common;
using ContentService.Sdk.Exceptions;
using Refit;

namespace ContentService.Sdk.Protocol;

/// <summary>
/// Translates <see cref="ApiException"/> instances from Refit pipelines into <see cref="ContentServiceApiException"/>
/// with bounded UTF-8 diagnostics and RFC 7807 <see cref="ContentServiceProblemDetails"/>.
/// </summary>
internal static class RefitExceptionMapper
{
        /// <summary>
        /// Converts a <see cref="ApiException"/> into a <see cref="ContentServiceApiException"/>.
        /// </summary>
        /// <param name="apiException">The Refit API exception to translate.</param>
        /// <returns>A mapped <see cref="ContentServiceApiException"/> preserving status code, problem details, and bounded body.</returns>
        public static ContentServiceApiException ToContentServiceApiException(ApiException apiException)
        {
                ArgumentNullException.ThrowIfNull(apiException);

                // Bound error payloads early because upstream reverse proxies can return multi-MB HTML error pages.
                string? boundedBody = TruncateUtf8WithinLimit(rawContent: apiException.Content);
                string? mediaType = apiException.ContentHeaders?.ContentType?.MediaType;
                ContentServiceProblemDetails? problemDetails = TryDeserializeProblemDetails(
                        mediaType: mediaType,
                        rawBody: boundedBody);

                string message = BuildErrorMessage(
                        apiException: apiException,
                        problemDetails: problemDetails,
                        rawBody: boundedBody);

                // Omit rawResponseBody when ProblemDetails parsed cleanly to avoid duplicating structured diagnostics.
                return new ContentServiceApiException(
                        statusCode: apiException.StatusCode,
                        message: message,
                        problemDetails: problemDetails,
                        rawResponseBody: problemDetails is null ? boundedBody : null,
                        innerException: apiException);
        }

        /// <summary>
        /// Truncates a string to at most <see cref="SdkConstants.MAX_ERROR_BODY_BYTES"/> UTF-8 bytes
        /// so oversized non-JSON error payloads do not bloat diagnostics.
        /// </summary>
        /// <param name="rawContent">The raw error response body.</param>
        /// <returns>The bounded error string, or <see langword="null"/> when empty.</returns>
        private static string? TruncateUtf8WithinLimit(string? rawContent)
        {
                if (string.IsNullOrEmpty(rawContent))
                {
                        return null;
                }

                byte[] utf8Bytes = Encoding.UTF8.GetBytes(rawContent);
                if (utf8Bytes.Length <= SdkConstants.MAX_ERROR_BODY_BYTES)
                {
                        return rawContent;
                }

                // Slicing at a fixed byte ceiling can split a trailing multi-byte UTF-8 sequence (yielding U+FFFD);
                // we accept that minor display artifact in exchange for a strict, predictable memory cap.
                return Encoding.UTF8.GetString(
                        bytes: utf8Bytes,
                        index: 0,
                        count: SdkConstants.MAX_ERROR_BODY_BYTES);
        }

        /// <summary>
        /// Attempts to deserialize RFC 7807 <see cref="ContentServiceProblemDetails"/> when the response is JSON.
        /// </summary>
        /// <param name="mediaType">The HTTP <c>Content-Type</c> media type.</param>
        /// <param name="rawBody">The bounded response body.</param>
        /// <returns>Deserialized problem details, or <see langword="null"/> for non-JSON or truncated/malformed JSON.</returns>
        private static ContentServiceProblemDetails? TryDeserializeProblemDetails(
                string? mediaType,
                string? rawBody)
        {
                if (string.IsNullOrWhiteSpace(rawBody) || !IsJsonMediaType(mediaType: mediaType))
                {
                        return null;
                }

                try
                {
                        return JsonSerializer.Deserialize<ContentServiceProblemDetails>(
                                json: rawBody,
                                options: SdkConstants.JSON_SERIALIZER_OPTIONS);
                }
                catch (JsonException)
                {
                        // Truncated or HTML-wrapped JSON falls back to RawResponseBody rather than masking the HTTP status.
                        return null;
                }
        }

        /// <summary>
        /// Determines whether the media type represents JSON or a <c>+json</c> structured syntax suffix (such as <c>application/problem+json</c>).
        /// </summary>
        /// <param name="mediaType">The response media type string.</param>
        /// <returns><see langword="true"/> if JSON-compatible; otherwise, <see langword="false"/>.</returns>
        private static bool IsJsonMediaType(string? mediaType)
        {
                if (string.IsNullOrWhiteSpace(mediaType))
                {
                        return false;
                }

                return string.Equals(a: mediaType, b: SdkConstants.PROBLEM_JSON_MEDIA_TYPE, comparisonType: StringComparison.OrdinalIgnoreCase)
                        || string.Equals(a: mediaType, b: SdkConstants.JSON_MEDIA_TYPE, comparisonType: StringComparison.OrdinalIgnoreCase)
                        || mediaType.EndsWith(value: SdkConstants.JSON_MEDIA_TYPE_SUFFIX, comparisonType: StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Builds a concise exception message prioritizing ProblemDetails <c>detail</c>, <c>title</c>, raw text, and HTTP reason phrase.
        /// </summary>
        /// <param name="apiException">The source Refit exception.</param>
        /// <param name="problemDetails">Parsed RFC 7807 details, if available.</param>
        /// <param name="rawBody">Bounded raw body text, if available.</param>
        /// <returns>A formatted diagnostic message.</returns>
        private static string BuildErrorMessage(
                ApiException apiException,
                ContentServiceProblemDetails? problemDetails,
                string? rawBody)
        {
                if (problemDetails is not null)
                {
                        string summary = !string.IsNullOrWhiteSpace(problemDetails.Detail)
                                ? problemDetails.Detail
                                : problemDetails.Title ?? apiException.ReasonPhrase ?? apiException.StatusCode.ToString();
                        return $"ContentService request failed with HTTP {(int)apiException.StatusCode}: {summary}";
                }

                if (!string.IsNullOrWhiteSpace(rawBody))
                {
                        return $"ContentService request failed with HTTP {(int)apiException.StatusCode}: {rawBody}";
                }

                string fallbackReason = string.IsNullOrWhiteSpace(apiException.ReasonPhrase)
                        ? apiException.StatusCode.ToString()
                        : apiException.ReasonPhrase;
                return $"ContentService request failed with HTTP {(int)apiException.StatusCode} ({fallbackReason}).";
        }
}

