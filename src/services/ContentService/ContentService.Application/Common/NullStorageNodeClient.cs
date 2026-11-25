using ContentService.Application.Interfaces;
using ContentService.Application.Models;

namespace ContentService.Application.Common;

/// <summary>
/// Fallback <see cref="IStorageNodeClient"/> used when Infrastructure (Dapr) is not registered (e.g., isolated unit tests).
/// </summary>
internal sealed class NullStorageNodeClient : IStorageNodeClient
{
        /// <inheritdoc />
        public Task<ImageMetadataResult?> GetImageMetadataAsync(
                string storageNodeId,
                Guid imageId,
                CancellationToken cancellationToken) =>
                Task.FromResult<ImageMetadataResult?>(null);

        /// <inheritdoc />
        public Task<IReadOnlyList<ImageMetadataResult>> GetImagesMetadataBatchAsync(
                string storageNodeId,
                IReadOnlyCollection<Guid> imageIds,
                CancellationToken cancellationToken) =>
                Task.FromResult<IReadOnlyList<ImageMetadataResult>>([]);

        /// <inheritdoc />
        public Task<ImageStreamResult?> OpenImageStreamAsync(
                string storageNodeId,
                Guid imageId,
                bool isPreview,
                CancellationToken cancellationToken) =>
                Task.FromResult<ImageStreamResult?>(null);

        /// <inheritdoc />
        public Task<ImageMetadataResult> UpdateImageMetadataAsync(
                string storageNodeId,
                UpdateImageMetadataCommand command,
                CancellationToken cancellationToken) =>
                throw new KeyNotFoundException($"Remote storage node '{storageNodeId}' is not reachable in standalone mode.");

        /// <inheritdoc />
        public Task DeleteImageAsync(
                string storageNodeId,
                DeleteImageCommand command,
                CancellationToken cancellationToken) =>
                throw new KeyNotFoundException($"Remote storage node '{storageNodeId}' is not reachable in standalone mode.");

        /// <inheritdoc />
        public Task<bool> IsImageReferencedByAnyTemplateAsync(
                string peerStorageNodeId,
                string imageStorageNodeId,
                Guid imageId,
                CancellationToken cancellationToken) =>
                Task.FromResult(false);
}

