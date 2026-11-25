using ContentService.Sdk.Common;
using ContentService.Sdk.Models;
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
                return new Uri(baseUri: normalizedBase, relativeUri: ResolveImagesRoutePrefix(options: options));
        }

        /// <summary>
        /// Builds the paginated image listing URI with optional search, album path, and recursion filters.
        /// </summary>
        /// <param name="httpClient">The configured <see cref="HttpClient"/> instance.</param>
        /// <param name="options">Fallback client options supplying <see cref="ContentServiceClientOptions.BaseAddress"/>.</param>
        /// <param name="query">The image catalog query parameters.</param>
        /// <returns>The resolved absolute catalog query <see cref="Uri"/>.</returns>
        public static Uri BuildImagesListUri(
                HttpClient httpClient,
                ContentServiceClientOptions options,
                ContentImageQuery query)
        {
                ArgumentNullException.ThrowIfNull(argument: query);

                Uri normalizedBase = ResolveNormalizedBaseUri(httpClient: httpClient, options: options);
                string queryString = $"{SdkConstants.PAGE_QUERY_PARAM}={query.Page}&{SdkConstants.PAGE_SIZE_QUERY_PARAM}={query.PageSize}";

                // Escape optional query parameters cleanly without adding ASP.NET Core query-builder package dependencies to the SDK.
                if (!string.IsNullOrWhiteSpace(value: query.SearchTerm))
                {
                        string trimmedSearch = query.SearchTerm.Trim();
                        queryString = $"{queryString}&{SdkConstants.SEARCH_TERM_QUERY_PARAM}={Uri.EscapeDataString(stringToEscape: trimmedSearch)}";
                }

                if (!string.IsNullOrWhiteSpace(value: query.AlbumPath))
                {
                        string trimmedAlbum = query.AlbumPath.Trim();
                        queryString = $"{queryString}&{SdkConstants.ALBUM_PATH_QUERY_PARAM}={Uri.EscapeDataString(stringToEscape: trimmedAlbum)}";
                        queryString = $"{queryString}&{SdkConstants.INCLUDE_SUBALBUMS_QUERY_PARAM}={(query.IncludeSubalbums ? "true" : "false")}";
                }

                return new Uri(baseUri: normalizedBase, relativeUri: $"{ResolveImagesRoutePrefix(options: options)}?{queryString}");
        }

        /// <summary>
        /// Builds the child album listing URI (<c>api/images/albums?parentPath=...</c>).
        /// </summary>
        /// <param name="httpClient">The configured <see cref="HttpClient"/> instance.</param>
        /// <param name="options">Fallback client options supplying <see cref="ContentServiceClientOptions.BaseAddress"/>.</param>
        /// <param name="parentPath">Optional parent album path.</param>
        /// <returns>The resolved absolute albums endpoint <see cref="Uri"/>.</returns>
        public static Uri BuildImageAlbumsUri(
                HttpClient httpClient,
                ContentServiceClientOptions options,
                string? parentPath)
        {
                Uri normalizedBase = ResolveNormalizedBaseUri(httpClient: httpClient, options: options);
                string route = $"{ResolveImagesRoutePrefix(options: options)}/{SdkConstants.IMAGE_ALBUMS_SUFFIX}";

                if (!string.IsNullOrWhiteSpace(value: parentPath))
                {
                        route = $"{route}?{SdkConstants.PARENT_PATH_QUERY_PARAM}={Uri.EscapeDataString(stringToEscape: parentPath.Trim())}";
                }

                return new Uri(baseUri: normalizedBase, relativeUri: route);
        }

        /// <summary>
        /// Builds the metadata update URI (<c>api/images/{imageId}/metadata</c>) after validating <paramref name="imageId"/>.
        /// </summary>
        /// <param name="httpClient">The configured <see cref="HttpClient"/> instance.</param>
        /// <param name="options">Fallback client options supplying <see cref="ContentServiceClientOptions.BaseAddress"/>.</param>
        /// <param name="imageId">The non-empty image identifier.</param>
        /// <returns>The resolved absolute metadata update <see cref="Uri"/>.</returns>
        public static Uri BuildImageMetadataUpdateUri(
                HttpClient httpClient,
                ContentServiceClientOptions options,
                Guid imageId)
        {
                ValidateImageId(imageId: imageId);
                Uri normalizedBase = ResolveNormalizedBaseUri(httpClient: httpClient, options: options);
                return new Uri(
                        baseUri: normalizedBase,
                        relativeUri: $"{ResolveImagesRoutePrefix(options: options)}/{imageId:D}/{SdkConstants.IMAGE_METADATA_SUFFIX}");
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
                return new Uri(baseUri: normalizedBase, relativeUri: $"{ResolveImagesRoutePrefix(options: options)}/{imageId:D}");
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
                        relativeUri: $"{ResolveImagesRoutePrefix(options: options)}/{imageId:D}/{SdkConstants.IMAGE_CONTENT_SUFFIX}");
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

        /// <summary>
        /// Resolves the versioned relative image prefix (<c>api/{version}/images</c>) from <see cref="ContentServiceClientOptions.ApiVersion"/>.
        /// </summary>
        private static string ResolveImagesRoutePrefix(ContentServiceClientOptions options)
        {
                string versionSegment = string.IsNullOrWhiteSpace(options.ApiVersion)
                        ? SdkConstants.DEFAULT_API_VERSION
                        : options.ApiVersion.Trim();

                return $"{SdkConstants.API_ROUTE_SEGMENT}/{versionSegment}/{SdkConstants.IMAGES_RESOURCE_SEGMENT}";
        }
}

