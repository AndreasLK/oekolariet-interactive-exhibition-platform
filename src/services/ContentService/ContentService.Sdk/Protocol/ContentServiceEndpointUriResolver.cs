using ContentService.Sdk.Common;
using ContentService.Sdk.Options;

namespace ContentService.Sdk.Protocol;

/// <summary>
/// Resolves and normalizes ContentService image display URIs (<c>/content</c> and <c>/preview</c>),
/// API version segments, and identifier validation while preserving configured base subpaths.
/// </summary>
internal static class ContentServiceEndpointUriResolver
{
        /// <summary>
        /// Builds the direct image binary content URI (<c>api/{version}/images/{imageIdentifier}/content</c>).
        /// </summary>
        /// <param name="httpClient">The configured <see cref="HttpClient"/> instance.</param>
        /// <param name="options">Fallback client options supplying <see cref="ContentServiceClientOptions.BaseAddress"/>.</param>
        /// <param name="imageIdentifier">The image GUID or shard-aware <c>{node}_{guid}</c> external identifier.</param>
        /// <returns>The resolved absolute binary content <see cref="Uri"/>.</returns>
        public static Uri BuildImageContentUri(
                HttpClient? httpClient,
                ContentServiceClientOptions options,
                string imageIdentifier)
        {
                string escapedId = Uri.EscapeDataString(stringToEscape: NormalizeIdentifier(identifier: imageIdentifier, paramName: nameof(imageIdentifier)));
                Uri normalizedBase = ResolveNormalizedBaseUri(httpClient: httpClient, options: options);
                string versionSegment = ResolveApiVersion(options: options);
                return new Uri(
                        baseUri: normalizedBase,
                        relativeUri: $"{SdkConstants.API_ROUTE_SEGMENT}/{versionSegment}/{SdkConstants.IMAGES_RESOURCE_SEGMENT}/{escapedId}/{SdkConstants.IMAGE_CONTENT_SUFFIX}");
        }

        /// <summary>
        /// Builds the lightweight image preview URI (<c>api/{version}/images/{imageIdentifier}/preview</c>).
        /// </summary>
        /// <param name="httpClient">The configured <see cref="HttpClient"/> instance.</param>
        /// <param name="options">Fallback client options supplying <see cref="ContentServiceClientOptions.BaseAddress"/>.</param>
        /// <param name="imageIdentifier">The image GUID or shard-aware <c>{node}_{guid}</c> external identifier.</param>
        /// <returns>The resolved absolute preview <see cref="Uri"/>.</returns>
        public static Uri BuildImagePreviewUri(
                HttpClient? httpClient,
                ContentServiceClientOptions options,
                string imageIdentifier)
        {
                string escapedId = Uri.EscapeDataString(stringToEscape: NormalizeIdentifier(identifier: imageIdentifier, paramName: nameof(imageIdentifier)));
                Uri normalizedBase = ResolveNormalizedBaseUri(httpClient: httpClient, options: options);
                string versionSegment = ResolveApiVersion(options: options);
                return new Uri(
                        baseUri: normalizedBase,
                        relativeUri: $"{SdkConstants.API_ROUTE_SEGMENT}/{versionSegment}/{SdkConstants.IMAGES_RESOURCE_SEGMENT}/{escapedId}/{SdkConstants.IMAGE_PREVIEW_SUFFIX}");
        }

        /// <summary>
        /// Resolves the configured API version segment, defaulting to <see cref="SdkConstants.DEFAULT_API_VERSION"/>.
        /// </summary>
        /// <param name="options">Client options supplying <see cref="ContentServiceClientOptions.ApiVersion"/>.</param>
        /// <returns>The trimmed API version segment (for example <c>v1</c>).</returns>
        public static string ResolveApiVersion(ContentServiceClientOptions options) =>
                string.IsNullOrWhiteSpace(options.ApiVersion)
                        ? SdkConstants.DEFAULT_API_VERSION
                        : options.ApiVersion.Trim();

        /// <summary>
        /// Rejects <see cref="Guid.Empty"/> before issuing HTTP calls or constructing resource URIs.
        /// </summary>
        /// <param name="id">The identifier to validate.</param>
        /// <param name="paramName">The caller parameter name for diagnostics.</param>
        public static void ValidateGuid(Guid id, string paramName)
        {
                if (id == Guid.Empty)
                {
                        throw new ArgumentException(
                                message: $"{paramName} cannot be empty.",
                                paramName: paramName);
                }
        }

        /// <summary>
        /// Validates and trims a GUID or shard-aware <c>{node}_{guid}</c> string identifier.
        /// </summary>
        /// <param name="identifier">The raw identifier string.</param>
        /// <param name="paramName">The caller parameter name for diagnostics.</param>
        /// <returns>The trimmed non-empty identifier.</returns>
        public static string NormalizeIdentifier(string identifier, string paramName)
        {
                ArgumentException.ThrowIfNullOrWhiteSpace(argument: identifier, paramName: paramName);
                return identifier.Trim();
        }

        /// <summary>
        /// Ensures an <see cref="HttpClient"/> has a normalized <see cref="HttpClient.BaseAddress"/> without a trailing slash
        /// for Refit so subpath base URLs (for example <c>https://host/content</c>) do not produce double slashes.
        /// </summary>
        /// <param name="httpClient">The <see cref="HttpClient"/> to configure.</param>
        /// <param name="options">Client options supplying fallback <see cref="ContentServiceClientOptions.BaseAddress"/>.</param>
        /// <returns>The same <paramref name="httpClient"/> instance with normalized <c>BaseAddress</c>.</returns>
        public static HttpClient EnsureNormalizedBaseAddress(
                HttpClient httpClient,
                ContentServiceClientOptions options)
        {
                ArgumentNullException.ThrowIfNull(httpClient);
                ArgumentNullException.ThrowIfNull(options);

                // Refit routes start with '/', so omit any trailing slash on BaseAddress to avoid '//' on subpaths.
                string trimmedBaseAddress = ResolveNormalizedBaseUri(httpClient: httpClient, options: options)
                        .AbsoluteUri
                        .TrimEnd(trimChar: '/');
                httpClient.BaseAddress = new Uri(uriString: trimmedBaseAddress, uriKind: UriKind.Absolute);
                return httpClient;
        }

        /// <summary>
        /// Rejects <see cref="Guid.Empty"/> before issuing HTTP calls or constructing template URIs.
        /// </summary>
        /// <param name="templateId">The template identifier to validate.</param>
        public static void ValidateTemplateId(Guid templateId)
        {
                if (templateId == Guid.Empty)
                {
                        throw new ArgumentException(
                                message: "TemplateId cannot be empty.",
                                paramName: nameof(templateId));
                }
        }

        /// <summary>
        /// Resolves an absolute base URI guaranteed to end with a trailing slash so RFC 3986 relative resolution keeps subpaths intact.
        /// </summary>
        /// <remarks>
        /// Non-standard asymmetry: <see cref="EnsureNormalizedBaseAddress"/> strips the trailing slash for Refit
        /// (whose routes begin with <c>/</c>), whereas <see cref="BuildImageContentUri"/> and <see cref="BuildImagePreviewUri"/>
        /// use <see cref="ResolveNormalizedBaseUri"/> with a trailing slash because <see cref="Uri(Uri, string)"/> drops
        /// the final path segment when the base URI lacks one (e.g., <c>https://host/content</c> + <c>api/v1/...</c>
        /// would otherwise drop <c>/content</c>).
        /// </remarks>
        /// <param name="httpClient">Optional <see cref="HttpClient"/> whose <c>BaseAddress</c> takes precedence.</param>
        /// <param name="options">Fallback client options supplying <see cref="ContentServiceClientOptions.BaseAddress"/>.</param>
        /// <returns>An absolute <see cref="Uri"/> ending with <c>/</c>.</returns>
        public static Uri ResolveNormalizedBaseUri(
                HttpClient? httpClient,
                ContentServiceClientOptions options)
        {
                Uri? baseAddress = httpClient?.BaseAddress ?? options.BaseAddress;
                if (baseAddress is null || !baseAddress.IsAbsoluteUri)
                {
                        throw new InvalidOperationException(
                                "ContentService BaseAddress must be configured as an absolute URI on HttpClient or ContentServiceClientOptions.");
                }

                string absolute = baseAddress.AbsoluteUri;
                return absolute.EndsWith('/')
                        ? baseAddress
                        : new Uri(uriString: $"{absolute}/", uriKind: UriKind.Absolute);
        }
}
