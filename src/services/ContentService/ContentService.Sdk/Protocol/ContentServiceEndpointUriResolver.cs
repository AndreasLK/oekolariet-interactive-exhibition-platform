using ContentService.Sdk.Common;
using ContentService.Sdk.Options;

namespace ContentService.Sdk.Protocol;

/// <summary>
/// Resolves and normalizes ContentService image endpoint URIs while preserving configured base subpaths.
/// </summary>
internal static class ContentServiceEndpointUriResolver
{
        /// <summary>
        /// Builds the upload endpoint URI (<c>api/images</c>) relative to the normalized base address.
        /// </summary>
        /// <param name="httpClient">The configured <see cref="HttpClient"/> instance.</param>
        /// <param name="options">Fallback client options supplying <see cref="ContentServiceClientOptions.BaseAddress"/>.</param>
        /// <returns>The resolved absolute upload endpoint <see cref="Uri"/>.</returns>
        public static Uri BuildImagesRootUri(
                HttpClient httpClient,
                ContentServiceClientOptions options)
        {
                Uri normalizedBase = ResolveNormalizedBaseUri(httpClient: httpClient, options: options);
                return new Uri(baseUri: normalizedBase, relativeUri: SdkConstants.IMAGES_ROUTE_PREFIX);
        }

        /// <summary>
        /// Builds the image metadata lookup URI (<c>api/images/{imageId}</c>) after validating <paramref name="imageId"/>.
        /// </summary>
        /// <param name="httpClient">The configured <see cref="HttpClient"/> instance.</param>
        /// <param name="options">Fallback client options supplying <see cref="ContentServiceClientOptions.BaseAddress"/>.</param>
        /// <param name="imageId">The non-empty image identifier.</param>
        /// <returns>The resolved absolute metadata endpoint <see cref="Uri"/>.</returns>
        public static Uri BuildImageMetadataUri(
                HttpClient httpClient,
                ContentServiceClientOptions options,
                Guid imageId)
        {
                ValidateImageId(imageId: imageId);
                Uri normalizedBase = ResolveNormalizedBaseUri(httpClient: httpClient, options: options);
                return new Uri(baseUri: normalizedBase, relativeUri: $"{SdkConstants.IMAGES_ROUTE_PREFIX}/{imageId:D}");
        }

        /// <summary>
        /// Builds the direct image binary content URI (<c>api/images/{imageId}/content</c>) after validating <paramref name="imageId"/>.
        /// </summary>
        /// <param name="httpClient">The configured <see cref="HttpClient"/> instance.</param>
        /// <param name="options">Fallback client options supplying <see cref="ContentServiceClientOptions.BaseAddress"/>.</param>
        /// <param name="imageId">The non-empty image identifier.</param>
        /// <returns>The resolved absolute binary content <see cref="Uri"/>.</returns>
        public static Uri BuildImageContentUri(
                HttpClient httpClient,
                ContentServiceClientOptions options,
                Guid imageId)
        {
                ValidateImageId(imageId: imageId);
                Uri normalizedBase = ResolveNormalizedBaseUri(httpClient: httpClient, options: options);
                return new Uri(
                        baseUri: normalizedBase,
                        relativeUri: $"{SdkConstants.IMAGES_ROUTE_PREFIX}/{imageId:D}/{SdkConstants.IMAGE_CONTENT_SUFFIX}");
        }

        /// <summary>
        /// Rejects <see cref="Guid.Empty"/> before issuing HTTP calls or constructing image URIs.
        /// </summary>
        /// <param name="imageId">The image identifier to validate.</param>
        public static void ValidateImageId(Guid imageId)
        {
                if (imageId == Guid.Empty)
                {
                        throw new ArgumentException(
                                message: "ImageId cannot be empty.",
                                paramName: nameof(imageId));
                }
        }

        /// <summary>
        /// Resolves an absolute base URI guaranteed to end with a trailing slash so RFC 3986 relative resolution keeps subpaths intact.
        /// </summary>
        /// <param name="httpClient">The configured <see cref="HttpClient"/> instance.</param>
        /// <param name="options">Fallback client options supplying <see cref="ContentServiceClientOptions.BaseAddress"/>.</param>
        /// <returns>The normalized absolute base <see cref="Uri"/>.</returns>
        private static Uri ResolveNormalizedBaseUri(
                HttpClient httpClient,
                ContentServiceClientOptions options)
        {
                Uri? baseAddress = httpClient.BaseAddress ?? options.BaseAddress;
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

