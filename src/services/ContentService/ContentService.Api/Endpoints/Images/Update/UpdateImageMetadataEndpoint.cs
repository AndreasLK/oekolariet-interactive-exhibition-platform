using ContentService.Api.Common;
using ContentService.Api.Mapper;
using ContentService.Application.Options;
using ContentService.Facade.Dtos;
using ContentService.Facade.Interfaces;
using ContentService.Sdk.Models;
using Microsoft.Extensions.Options;

namespace ContentService.Api.Endpoints.Images;

/// <summary>
/// Handles partial image metadata updates (<c>PATCH /api/v1/images/{imageIdentifier}/metadata</c>).
/// </summary>
internal static class UpdateImageMetadataEndpoint
{
        /// <summary>
        /// Updates searchable description (<c>AltText</c>), tags, and album path for an active image while enforcing optimistic concurrency.
        /// </summary>
        /// <param name="imageIdentifier">The bare GUID or shard-aware <c>{node}_{guid}</c> identifier.</param>
        /// <param name="request">The metadata patch payload.</param>
        /// <param name="httpContext">The active HTTP context.</param>
        /// <param name="storageOptions">Configured local storage options.</param>
        /// <param name="commandFacade">The image command facade.</param>
        /// <param name="cancellationToken">Propagates client disconnects.</param>
        /// <returns>An HTTP 200 OK response containing the updated <see cref="ContentImageResponse"/>.</returns>
        public static async Task<IResult> HandleAsync(
                string imageIdentifier,
                ContentUpdateImageMetadataRequest request,
                HttpContext httpContext,
                IOptions<ContentStorageOptions> storageOptions,
                IImageCommandFacade commandFacade,
                CancellationToken cancellationToken)
        {
                // Parse bare GUID vs. node-qualified ID and check X-Node-Forwarded loop guard.
                (string? storageNodeId, Guid imageId, bool allowPeerFallback) = ImageRouteTargetParser.Parse(
                        imageIdentifier: imageIdentifier,
                        httpContext: httpContext,
                        localStorageNodeId: storageOptions.Value.StorageNodeId);

                // Map the PATCH body, concurrency stamp, and actor header into the Facade command DTO.
                string actorId = RequestActorResolver.ResolveActorId(
                        httpContext: httpContext,
                        explicitActorId: request.ActorId);

                UpdateImageMetadataCommandDto command = ImageMapper.ToCommand(
                        request: request,
                        imageId: imageId,
                        storageNodeId: storageNodeId,
                        allowPeerFallback: allowPeerFallback,
                        actorId: actorId);

                // Apply the metadata update (locally or on the owning peer) and return the mapped SDK response.
                ImageMetadataDto updated = await commandFacade.UpdateImageMetadataAsync(
                        command: command,
                        cancellationToken: cancellationToken);

                return Results.Ok(value: ImageMapper.ToResponse(dto: updated));
        }
}

