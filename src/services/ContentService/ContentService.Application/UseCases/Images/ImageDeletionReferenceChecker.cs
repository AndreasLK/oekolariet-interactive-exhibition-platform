using ContentService.Application.Common;
using ContentService.Application.Interfaces;
using ContentService.Application.Options;

namespace ContentService.Application.UseCases.Images;

/// <summary>
/// Verifies whether an image is referenced by any active or archived template on the local node or configured peer nodes.
/// </summary>
internal static class ImageDeletionReferenceChecker
{
        /// <summary>
        /// Checks both the local template-reference index and configured peer nodes before allowing deletion.
        /// </summary>
        /// <param name="imageRepository">The local image metadata repository.</param>
        /// <param name="storageNodeClient">Optional peer storage node client.</param>
        /// <param name="options">Optional content storage options.</param>
        /// <param name="imageStorageNodeId">The storage node identifier owning the image.</param>
        /// <param name="imageId">The image identifier to check.</param>
        /// <param name="cancellationToken">Propagates cancellation.</param>
        /// <returns><see langword="true"/> if any template locally or on a peer references the image; otherwise <see langword="false"/>.</returns>
        public static async Task<bool> IsReferencedLocallyOrOnPeersAsync(
                IImageRepository imageRepository,
                IStorageNodeClient? storageNodeClient,
                ContentStorageOptions? options,
                string imageStorageNodeId,
                Guid imageId,
                CancellationToken cancellationToken)
        {
                bool isReferencedLocally = await imageRepository.IsReferencedByAnyTemplateAsync(
                        imageId: imageId,
                        cancellationToken: cancellationToken);
                if (isReferencedLocally)
                {
                        return true;
                }

                if (storageNodeClient is null || options?.PeerStorageNodeIds is null)
                {
                        return false;
                }

                // WHY: Because the SQL FK on TemplateImageReferences was removed to allow cross-node templates,
                // we must ask peer nodes whether any of their templates reference this local image before soft-deleting it.
                foreach (string peerNodeId in options.PeerStorageNodeIds)
                {
                        if (!StorageNodeTargetResolver.IsRemoteNode(
                                targetStorageNodeId: peerNodeId,
                                localStorageNodeId: options.StorageNodeId))
                        {
                                continue;
                        }

                        bool isReferencedOnPeer = await storageNodeClient.IsImageReferencedByAnyTemplateAsync(
                                peerStorageNodeId: peerNodeId,
                                imageStorageNodeId: imageStorageNodeId,
                                imageId: imageId,
                                cancellationToken: cancellationToken);
                        if (isReferencedOnPeer)
                        {
                                return true;
                        }
                }

                return false;
        }
}

