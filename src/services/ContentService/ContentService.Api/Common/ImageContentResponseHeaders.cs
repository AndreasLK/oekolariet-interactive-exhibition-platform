using ContentService.Facade.Dtos;
using Microsoft.Net.Http.Headers;

namespace ContentService.Api.Common;

/// <summary>
/// Parses multipart media types and configures security/caching response headers for image content downloads.
/// </summary>
internal static class ImageContentResponseHeaders
{
        /// <summary>
        /// Strips optional MIME parameters (such as <c>; charset=utf-8</c>) so valid uploads are not falsely rejected by exact allowlist comparisons.
        /// </summary>
        /// <param name="rawContentType">The raw multipart <c>Content-Type</c> header value.</param>
        /// <returns>The bare media type string, or the raw value when unparseable so downstream validation reports the bad header.</returns>
        public static string ExtractMediaType(string? rawContentType)
        {
                if (string.IsNullOrWhiteSpace(rawContentType))
                {
                        return string.Empty;
                }

                return MediaTypeHeaderValue.TryParse(input: rawContentType, parsedValue: out MediaTypeHeaderValue? parsed)
                        ? parsed.MediaType.Value ?? string.Empty
                        : rawContentType;
        }

        /// <summary>
        /// Sets immutable caching, content length, range-support notice, MIME-sniffing protection, UTF-8 safe inline disposition, and <c>Vary: Origin</c>.
        /// </summary>
        /// <param name="response">The outgoing HTTP response.</param>
        /// <param name="metadata">The image metadata describing size and file name.</param>
        public static void Configure(
                HttpResponse response,
                ImageMetadataDto metadata)
        {
                // Image bytes are immutable per ID (metadata edits do not alter bytes; replacements get a new ID), so 1-year immutable caching avoids 304 revalidation round-trips on kiosks.
                response.Headers.CacheControl = ApiConstants.IMAGE_CACHE_CONTROL_HEADER_VALUE;
                response.ContentLength = metadata.SizeBytes;

                // Advertise no byte-range support because images are served as full blobs rather than random-access range streams.
                response.Headers[HeaderNames.AcceptRanges] = ApiConstants.ACCEPT_RANGES_NONE;

                // Prevent browsers from MIME-sniffing binary image responses into executable HTML or script.
                response.Headers[HeaderNames.XContentTypeOptions] = ApiConstants.CONTENT_TYPE_OPTIONS_NOSNIFF;

                // Use FileNameStar so RFC 6266 UTF-8 percent-encoding preserves spaces and Danish/non-ASCII characters safely in HTTP headers.
                ContentDispositionHeaderValue disposition = new(ApiConstants.INLINE_DISPOSITION_TYPE)
                {
                        FileNameStar = metadata.FileName
                };
                response.Headers[HeaderNames.ContentDisposition] = disposition.ToString();

                // Plain <img> tags request images without an Origin header, so ASP.NET Core CORS middleware
                // skips them and omits both Access-Control-Allow-Origin and Vary: Origin.
                // Adding Vary: Origin when no Origin header was sent tells the browser cache to keep
                // <img> responses separate from CORS fetch() responses (preventing "TypeError: Failed to fetch").
                // When an Origin header IS sent, CORS middleware adds Vary: Origin itself after the endpoint runs.
                bool hasOriginHeader = response.HttpContext.Request.Headers.ContainsKey(HeaderNames.Origin);
                if (!hasOriginHeader && !response.Headers.Vary.Contains(value: HeaderNames.Origin, comparer: StringComparer.OrdinalIgnoreCase))
                {
                        response.Headers.Append(key: HeaderNames.Vary, value: HeaderNames.Origin);
                }
        }
}

