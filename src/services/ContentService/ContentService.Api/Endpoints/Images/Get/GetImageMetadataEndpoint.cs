using ContentService.Api.Mapper;
using ContentService.Application.Options;
using ContentService.Facade.Dtos;
using ContentService.Facade.Interfaces;
using ContentService.Sdk.Models;
using Microsoft.Extensions.Options;

namespace ContentService.Api.Endpoints.Images;

/// <summary>
/// Handles single-image metadata lookups (<c>GET /api/v1/images/{imageIdentifier}</c>).
/// </summary>
internal static class GetImageMetadataEndpoint
{
        /// <summary>
        /// Exposes image metadata independently of <c>/content</c> so editors and publishers can inspect file size, alt text, and SHA-256 digests without transferring image bytes.
        /// </summary>
        /// <param name="imageIdentifier">The bare GUID or shard-aware <c>{node}_{guid}</c> identifier.</param>
        /// <param name="httpContext">The active HTTP context.</param>
        /// <param name="storageOptions">Configured local storage options.</param>
        /// <param name="queryFacade">The image query facade.</param>
        /// <param name="cancellationToken">Propagates client disconnects.</param>
        /// <returns>An HTTP 200 OK response containing the <see cref="ContentImageResponse"/>.</returns>
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

                // Fetch image metadata locally or via peer forwarding, then return the mapped SDK response.
                ImageMetadataDto metadata = await queryFacade.GetImageMetadataAsync(
                        imageId: imageId,
                        cancellationToken: cancellationToken,
                        storageNodeId: storageNodeId,
                        allowPeerFallback: allowPeerFallback);

                return Results.Ok(value: ImageMapper.ToResponse(dto: metadata));
        }
}

