using System.Net;
using System.Text.Json;
using ContentService.Domain.Exceptions;

namespace ContentService.Infrastructure.Nodes;

/// <summary>
/// Maps remote RFC 7807 <c>ProblemDetails</c> responses from peer storage nodes into local domain exceptions.
/// </summary>
internal static class DaprNodeFailureMapper
{
        /// <summary>
        /// Translates remote RFC 7807 <c>ProblemDetails</c> failures back into local domain exceptions so <c>GlobalExceptionHandler</c> preserves 400/404/409 semantics.
        /// </summary>
        /// <param name="response">The upstream HTTP response from the peer storage node.</param>
        /// <param name="cancellationToken">Propagates cancellation while reading error details.</param>
        /// <param name="conflictAsEntityInUse">When <see langword="true"/> (e.g., DELETE), maps 409 Conflict to <see cref="EntityInUseException"/>; otherwise maps to <see cref="ConcurrencyConflictException"/>.</param>
        public static async Task EnsureSuccessOrThrowDomainAsync(
                HttpResponseMessage response,
                CancellationToken cancellationToken,
                bool conflictAsEntityInUse = false)
        {
                if (response.IsSuccessStatusCode)
                {
                        return;
                }

                string detail = await TryReadProblemDetailAsync(response: response, cancellationToken: cancellationToken);

                throw response.StatusCode switch
                {
                        HttpStatusCode.BadRequest => new DomainValidationException(detail),
                        HttpStatusCode.NotFound => new KeyNotFoundException(detail),
                        HttpStatusCode.Conflict => conflictAsEntityInUse
                                ? new EntityInUseException(detail)
                                : new ConcurrencyConflictException(detail),
                        _ => new HttpRequestException(
                                message: detail,
                                inner: null,
                                statusCode: response.StatusCode)
                };
        }

        /// <summary>
        /// Extracts the human-readable <c>detail</c> or <c>title</c> from a remote ProblemDetails JSON body.
        /// </summary>
        /// <param name="response">The failed HTTP response.</param>
        /// <param name="cancellationToken">Propagates cancellation while reading the response body.</param>
        /// <returns>The extracted detail string, or a fallback status message when the body is empty or non-JSON.</returns>
        private static async Task<string> TryReadProblemDetailAsync(
                HttpResponseMessage response,
                CancellationToken cancellationToken)
        {
                string rawBody = await response.Content.ReadAsStringAsync(cancellationToken: cancellationToken);
                if (string.IsNullOrWhiteSpace(rawBody))
                {
                        return $"Remote storage node returned HTTP {(int)response.StatusCode}.";
                }

                try
                {
                        using JsonDocument document = JsonDocument.Parse(rawBody);
                        if (document.RootElement.TryGetProperty(propertyName: "detail", value: out JsonElement detailElement)
                                && detailElement.ValueKind == JsonValueKind.String)
                        {
                                return detailElement.GetString() ?? rawBody;
                        }

                        if (document.RootElement.TryGetProperty(propertyName: "title", value: out JsonElement titleElement)
                                && titleElement.ValueKind == JsonValueKind.String)
                        {
                                return titleElement.GetString() ?? rawBody;
                        }
                }
                catch (JsonException)
                {
                        // Fall back to the raw text when the upstream error body is not JSON.
                }

                return rawBody;
        }
}

