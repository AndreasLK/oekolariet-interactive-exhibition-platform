using System.Net;
using System.Text;
using System.Text.Json;
using ContentService.Sdk.Common;
using ContentService.Sdk.Exceptions;

namespace ContentService.Sdk.Protocol;

/// <summary>
/// Translates non-success HTTP responses from ContentService into <see cref="ContentServiceApiException"/> instances with bounded diagnostics.
/// </summary>
internal static class HttpResponseErrorMapper
{
        /// <summary>
        /// Verifies that <paramref name="response"/> succeeded, or reads bounded diagnostics and throws <see cref="ContentServiceApiException"/>.
        /// </summary>
        /// <param name="response">The HTTP response to inspect (lifetime remains owned by the caller).</param>
        /// <param name="cancellationToken">A token to cancel error body reading.</param>
        /// <returns>A completed task when the response status code indicates success.</returns>
        /// <exception cref="ContentServiceApiException">Thrown when <paramref name="response"/> has a non-2xx status code.</exception>
        public static async Task EnsureSuccessOrThrowAsync(
                HttpResponseMessage response,
                CancellationToken cancellationToken)
        {
                if (response.IsSuccessStatusCode)
                {
                        return;
                }

                // Read a bounded diagnostic snippet without disposing response; callers manage HttpResponseMessage lifetime.
                (string? rawBody, Exception? readException) = await TryReadBoundedErrorBodyAsync(
                        content: response.Content,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                ContentServiceProblemDetails? problemDetails = TryDeserializeProblemDetails(
                        mediaType: response.Content.Headers.ContentType?.MediaType,
                        rawBody: rawBody);

                string message = BuildErrorMessage(
                        statusCode: response.StatusCode,
                        reasonPhrase: response.ReasonPhrase,
                        problemDetails: problemDetails,
                        rawBody: rawBody);

                throw new ContentServiceApiException(
                        statusCode: response.StatusCode,
                        message: message,
                        problemDetails: problemDetails,
                        rawResponseBody: problemDetails is null ? rawBody : null,
                        innerException: readException);
        }

        /// <summary>
        /// Reads at most <see cref="SdkConstants.MAX_ERROR_BODY_BYTES"/> from an error body while preserving caller cancellation and HTTP status on I/O faults.
        /// </summary>
        /// <param name="content">The error HTTP content, if any.</param>
        /// <param name="cancellationToken">A token to cancel reading.</param>
        /// <returns>The bounded UTF-8 text body and any non-cancellation I/O exception encountered.</returns>
        private static async Task<(string? Body, Exception? ReadException)> TryReadBoundedErrorBodyAsync(
                HttpContent? content,
                CancellationToken cancellationToken)
        {
                if (content is null)
                {
                        return (null, null);
                }

                try
                {
                        await using Stream stream = await content.ReadAsStreamAsync(
                                cancellationToken: cancellationToken).ConfigureAwait(false);
                        string? body = await ReadUtf8WithinLimitAsync(
                                stream: stream,
                                maxBytes: SdkConstants.MAX_ERROR_BODY_BYTES,
                                cancellationToken: cancellationToken).ConfigureAwait(false);
                        return (body, null);
                }
                catch (OperationCanceledException)
                {
                        throw;
                }
                catch (Exception exception)
                {
                        // Preserve the underlying HTTP status code if reading a broken error stream fails.
                        return (null, exception);
                }
        }

        /// <summary>
        /// Reads up to <paramref name="maxBytes"/> bytes from <paramref name="stream"/> and decodes them as UTF-8.
        /// </summary>
        /// <param name="stream">The error stream to read.</param>
        /// <param name="maxBytes">The hard byte ceiling.</param>
        /// <param name="cancellationToken">A token to cancel reading.</param>
        /// <returns>The decoded text, or <see langword="null"/> when empty.</returns>
        private static async Task<string?> ReadUtf8WithinLimitAsync(
                Stream stream,
                int maxBytes,
                CancellationToken cancellationToken)
        {
                byte[] buffer = new byte[maxBytes];
                int totalRead = 0;

                while (totalRead < buffer.Length)
                {
                        int bytesRead = await stream.ReadAsync(
                                buffer: buffer.AsMemory(start: totalRead, length: buffer.Length - totalRead),
                                cancellationToken: cancellationToken).ConfigureAwait(false);
                        if (bytesRead == 0)
                        {
                                break;
                        }

                        totalRead += bytesRead;
                }

                return totalRead == 0
                        ? null
                        : Encoding.UTF8.GetString(bytes: buffer, index: 0, count: totalRead);
        }

        /// <summary>
        /// Deserializes RFC 7807 problem details when the response carries a JSON media type.
        /// </summary>
        /// <param name="mediaType">The response media type.</param>
        /// <param name="rawBody">The bounded raw response body.</param>
        /// <returns>Parsed <see cref="ContentServiceProblemDetails"/>, or <see langword="null"/> for non-JSON or malformed payloads.</returns>
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
                        // Fall back to rawResponseBody when a truncated or proxy-corrupted JSON payload cannot deserialize.
                        return null;
                }
        }

        /// <summary>
        /// Determines whether the HTTP media type represents JSON or ProblemDetails JSON.
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
        /// Formats a concise diagnostic exception message prioritizing ProblemDetails detail, title, and raw text.
        /// </summary>
        /// <param name="statusCode">The HTTP status code.</param>
        /// <param name="reasonPhrase">The HTTP reason phrase.</param>
        /// <param name="problemDetails">Parsed RFC 7807 payload, if available.</param>
        /// <param name="rawBody">Bounded raw response body, if available.</param>
        /// <returns>A human-readable failure summary.</returns>
        private static string BuildErrorMessage(
                HttpStatusCode statusCode,
                string? reasonPhrase,
                ContentServiceProblemDetails? problemDetails,
                string? rawBody)
        {
                if (problemDetails is not null)
                {
                        string summary = !string.IsNullOrWhiteSpace(problemDetails.Detail)
                                ? problemDetails.Detail
                                : problemDetails.Title ?? reasonPhrase ?? statusCode.ToString();
                        return $"ContentService request failed with HTTP {(int)statusCode}: {summary}";
                }

                if (!string.IsNullOrWhiteSpace(rawBody))
                {
                        return $"ContentService request failed with HTTP {(int)statusCode}: {rawBody}";
                }

                string fallbackReason = string.IsNullOrWhiteSpace(reasonPhrase)
                        ? statusCode.ToString()
                        : reasonPhrase;
                return $"ContentService request failed with HTTP {(int)statusCode} ({fallbackReason}).";
        }
}

