using ContentService.Application.Models;

namespace ContentService.Application.Interfaces;

/// <summary>
/// Port for invoking peer <c>ContentService</c> storage nodes (typically over Dapr service invocation) when an image or template reference belongs to another node.
/// </summary>
public interface IStorageNodeClient
{
        /// <summary>
        /// Fetches metadata for a single active image from a remote storage node.
        /// </summary>
        /// <param name="storageNodeId">The target storage node identifier.</param>
        /// <param name="imageId">The unique image identifier.</param>
        /// <param name="cancellationToken">Propagates cancellation to the remote call.</param>
        /// <returns>The <see cref="ImageMetadataResult"/>, or <see langword="null"/> if not found on that node.</returns>
        public Task<ImageMetadataResult?> GetImageMetadataAsync(
                string storageNodeId,
                Guid imageId,
                CancellationToken cancellationToken);

        /// <summary>
        /// Batch-fetches active image metadata for multiple IDs from a single remote storage node in one round-trip.
        /// </summary>
        /// <param name="storageNodeId">The target storage node identifier.</param>
        /// <param name="imageIds">The image identifiers to resolve on that node.</param>
        /// <param name="cancellationToken">Propagates cancellation to the remote call.</param>
        /// <returns>The resolved active <see cref="ImageMetadataResult"/> items found on that node.</returns>
        public Task<IReadOnlyList<ImageMetadataResult>> GetImagesMetadataBatchAsync(
                string storageNodeId,
                IReadOnlyCollection<Guid> imageIds,
                CancellationToken cancellationToken);

        /// <summary>
        /// Opens a streaming image payload (full content or lightweight preview) from a remote storage node.
        /// </summary>
        /// <param name="storageNodeId">The target storage node identifier.</param>
        /// <param name="imageId">The unique image identifier.</param>
        /// <param name="isPreview"><see langword="true"/> to request the preview stream; <see langword="false"/> for full content.</param>
        /// <param name="cancellationToken">Propagates cancellation to the remote call.</param>
        /// <returns>An <see cref="ImageStreamResult"/> owning the remote HTTP stream, or <see langword="null"/> if not found.</returns>
        public Task<ImageStreamResult?> OpenImageStreamAsync(
                string storageNodeId,
                Guid imageId,
                bool isPreview,
                CancellationToken cancellationToken);

        /// <summary>
        /// Forwards a metadata update command to the owning remote storage node.
        /// </summary>
        /// <param name="storageNodeId">The owning storage node identifier.</param>
        /// <param name="command">The metadata update command.</param>
        /// <param name="cancellationToken">Propagates cancellation to the remote call.</param>
        /// <returns>The updated <see cref="ImageMetadataResult"/>.</returns>
        public Task<ImageMetadataResult> UpdateImageMetadataAsync(
                string storageNodeId,
                UpdateImageMetadataCommand command,
                CancellationToken cancellationToken);

        /// <summary>
        /// Forwards a soft-delete command to the owning remote storage node.
        /// </summary>
        /// <param name="storageNodeId">The owning storage node identifier.</param>
        /// <param name="command">The delete command.</param>
        /// <param name="cancellationToken">Propagates cancellation to the remote call.</param>
        /// <returns>A task representing the asynchronous remote deletion.</returns>
        public Task DeleteImageAsync(
                string storageNodeId,
                DeleteImageCommand command,
                CancellationToken cancellationToken);

        /// <summary>
        /// Checks whether any template on a peer storage node still references a given image.
        /// </summary>
        /// <param name="peerStorageNodeId">The peer storage node hosting templates to inspect.</param>
        /// <param name="imageStorageNodeId">The storage node owning the image being checked.</param>
        /// <param name="imageId">The image identifier being checked.</param>
        /// <param name="cancellationToken">Propagates cancellation to the remote call.</param>
        /// <returns><see langword="true"/> if any template on <paramref name="peerStorageNodeId"/> references the image; otherwise <see langword="false"/>.</returns>
        public Task<bool> IsImageReferencedByAnyTemplateAsync(
                string peerStorageNodeId,
                string imageStorageNodeId,
                Guid imageId,
                CancellationToken cancellationToken);
}

