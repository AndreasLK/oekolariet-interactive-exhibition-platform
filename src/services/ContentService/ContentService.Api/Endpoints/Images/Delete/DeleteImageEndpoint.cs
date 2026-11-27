using ContentService.Api.Common;
using ContentService.Application.Options;
using ContentService.Facade.Dtos;
using ContentService.Facade.Interfaces;
using Microsoft.Extensions.Options;

namespace ContentService.Api.Endpoints.Images;

/// <summary>
/// Handles soft-deletion of exhibition images (<c>DELETE /api/v1/images/{imageIdentifier}</c>).
/// </summary>
internal static class DeleteImageEndpoint
{
        /// <summary>
        /// Soft-deletes an unreferenced image so storage quota is reclaimed while active template links remain protected by <c>409 Conflict</c>.
        /// </summary>
        /// <param name="imageIdentifier">The bare GUID or shard-aware <c>{node}_{guid}</c> identifier.</param>
        /// <param name="httpContext">The active HTTP context.</param>
        /// <param name="storageOptions">Configured local storage options.</param>
        /// <param name="commandFacade">The image command facade.</param>
        /// <param name="cancellationToken">Propagates client disconnects.</param>
        /// <returns>An HTTP 204 No Content response.</returns>
        public static async Task<IResult> HandleAsync(
                string imageIdentifier,
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

                // Build the delete command with the resolved actor ID for audit logging.
                DeleteImageCommandDto command = new()
                {
                        ImageId = imageId,
                        StorageNodeId = storageNodeId,
                        AllowPeerFallback = allowPeerFallback,
                        ActorId = RequestActorResolver.ResolveActorId(httpContext: httpContext)
                };

                // Execute soft-delete (or forward to owning peer) and return 204 No Content.
                await commandFacade.DeleteImageAsync(command: command, cancellationToken: cancellationToken);
                return Results.NoContent();
        }
}

