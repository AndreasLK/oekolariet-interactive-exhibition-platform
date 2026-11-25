using ContentService.Application.Options;
using ContentService.Facade.Dtos;
using ContentService.Facade.Interfaces;
using Microsoft.Extensions.Options;

namespace ContentService.Api.Endpoints.Images;

/// <summary>
/// Handles lightweight image preview streaming (<c>GET /api/v1/images/{imageIdentifier}/preview</c>).
/// </summary>
internal static class GetImagePreviewEndpoint
{
        /// <summary>
        /// Streams the saved lightweight preview image with immutable caching headers, generating and persisting it on first access if absent.
        /// </summary>
        /// <param name="imageIdentifier">The bare GUID or shard-aware <c>{node}_{guid}</c> identifier.</param>
        /// <param name="httpContext">The active HTTP context.</param>
        /// <param name="storageOptions">Configured local storage options.</param>
        /// <param name="queryFacade">The image query facade.</param>
        /// <param name="cancellationToken">Propagates client disconnects.</param>
        /// <returns>A streaming HTTP 200 OK preview response.</returns>
        public static async Task<IResult> HandleAsync(
                string imageIdentifier,
                HttpContext httpContext,
                IOptions<ContentStorageOptions> storageOptions,
                IImageQueryFacade queryFacade,
                CancellationToken cancellationToken)
        {
                // Parse bare GUID vs. node-qualified ID and check X-Node-Forwarded loop guard.
                (string? storageNodeId, Guid imageId, bool allowPeerFallback) = ImageRouteTargetParser.Parse(
                        imageIdentifier: imageIdentifier,
                        httpContext: httpContext,
                        localStorageNodeId: storageOptions.Value.StorageNodeId);

                // Fetch (or lazily backfill) the lightweight preview stream from local or peer storage.
                ImageStreamDto streamDto = await queryFacade.GetImagePreviewAsync(
                        imageId: imageId,
                        cancellationToken: cancellationToken,
                        storageNodeId: storageNodeId,
                        allowPeerFallback: allowPeerFallback);

                // Stream the preview bytes with immutable cache headers and transfer disposal ownership to ASP.NET Core.
                return await ImageStreamResponseWriter.WriteAsync(streamDto: streamDto, httpContext: httpContext);
        }
}

