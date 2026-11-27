using ContentService.Application.Common;
using ContentService.Application.Interfaces;
using ContentService.Application.Models;
using ContentService.Application.Options;
using ContentService.Domain.Entities;
using ContentService.Domain.Exceptions;
using Microsoft.Extensions.Options;

namespace ContentService.Application.UseCases.Images;

/// <summary>
/// Serves a saved lightweight preview stream for an active image, generating and persisting it on first access if absent.
/// </summary>
public sealed class GetImagePreviewUseCase
{
        private readonly IImageRepository _imageRepository;
        private readonly IImageBinaryStore _imageBinaryStore;
        private readonly IImagePreviewGenerator? _previewGenerator;
        private readonly IStorageNodeClient? _storageNodeClient;
        private readonly ContentStorageOptions? _options;
        private readonly IPoolCoordinator? _poolCoordinator;

        /// <summary>
        /// Captures repository, binary store, and preview generator dependencies.
        /// </summary>
        /// <param name="imageRepository">The image metadata repository.</param>
        /// <param name="imageBinaryStore">The binary and preview stream store.</param>
        /// <param name="previewGenerator">Optional preview generator for lazy backfill.</param>
        /// <param name="storageNodeClient">Optional peer storage node client.</param>
        /// <param name="options">Optional content storage options.</param>
        /// <param name="poolCoordinator">Optional stateless Dapr Pub/Sub pool coordinator.</param>
        public GetImagePreviewUseCase(
                IImageRepository imageRepository,
                IImageBinaryStore imageBinaryStore,
                IImagePreviewGenerator? previewGenerator = null,
                IStorageNodeClient? storageNodeClient = null,
                IOptions<ContentStorageOptions>? options = null,
                IPoolCoordinator? poolCoordinator = null)
        {
                ArgumentNullException.ThrowIfNull(imageRepository);
                ArgumentNullException.ThrowIfNull(imageBinaryStore);

                this._imageRepository = imageRepository;
                this._imageBinaryStore = imageBinaryStore;
                this._previewGenerator = previewGenerator;
                this._storageNodeClient = storageNodeClient;
                this._options = options?.Value;
                this._poolCoordinator = poolCoordinator;
        }

        /// <summary>
        /// Returns the saved preview stream for <paramref name="query"/>, generating and saving it on first request if missing.
        /// </summary>
        /// <param name="query">The image query identifying the target image.</param>
        /// <param name="cancellationToken">A token to cancel database reads or preview generation.</param>
        /// <returns>An <see cref="ImageStreamResult"/> carrying the preview stream and effective MIME type.</returns>
        public async Task<ImageStreamResult> ExecuteAsync(
                GetImageStreamQuery query,
                CancellationToken cancellationToken)
        {
                ArgumentNullException.ThrowIfNull(query);

                if (query.ImageId == Guid.Empty)
                {
                        throw new DomainValidationException("Image ID cannot be empty.");
                }

                if (this._storageNodeClient is not null
                        && StorageNodeTargetResolver.IsRemoteNode(
                                targetStorageNodeId: query.StorageNodeId,
                                localStorageNodeId: this._options?.StorageNodeId))
                {
                        ImageStreamResult? remotePreview = await this._storageNodeClient.OpenImageStreamAsync(
                                storageNodeId: query.StorageNodeId!,
                                imageId: query.ImageId,
                                isPreview: true,
                                cancellationToken: cancellationToken);

                        return remotePreview
                                ?? throw new KeyNotFoundException($"Image '{query.StorageNodeId}_{query.ImageId}' was not found.");
                }

                ImageEntity? image = await this._imageRepository.GetByIdAsync(
                        id: query.ImageId,
                        cancellationToken: cancellationToken);
                if (image is null || image.IsDeleted)
                {
                        ImageStreamResult? peerPreview = await this.TryOpenPeerPreviewAsync(
                                query: query,
                                cancellationToken: cancellationToken);

                        return peerPreview
                                ?? throw new KeyNotFoundException($"Image '{query.ImageId}' was not found.");
                }

                ImageMetadataResult metadata = ContentResultMapper.ToMetadataResult(image: image);

                // Fast path: serve the already-saved preview directly without decoding or resizing.
                ImagePreviewStreamResult? existingPreview = await this._imageBinaryStore.OpenPreviewReadAsync(
                        imageId: image.Id,
                        cancellationToken: cancellationToken);
                if (existingPreview is not null)
                {
                        ImageMetadataResult previewMetadata = metadata with
                        {
                                ContentType = existingPreview.ContentType,
                                SizeBytes = existingPreview.SizeBytes
                        };
                        return new ImageStreamResult(metadata: previewMetadata, content: existingPreview.Content);
                }

                return await ImagePreviewBackfiller.BackfillOrFallbackAsync(
                        imageBinaryStore: this._imageBinaryStore,
                        previewGenerator: this._previewGenerator,
                        metadata: metadata,
                        cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Probes peer nodes via Dapr when a bare-GUID preview is absent locally.
        /// </summary>
        private Task<ImageStreamResult?> TryOpenPeerPreviewAsync(
                GetImageStreamQuery query,
                CancellationToken cancellationToken)
        {
                if (this._storageNodeClient is null || query.StorageNodeId is not null)
                {
                        return Task.FromResult<ImageStreamResult?>(null);
                }

                return StorageNodeTargetResolver.ResolveAcrossPoolAsync(
                        imageId: query.ImageId,
                        options: this._options,
                        allowPeerFallback: query.AllowPeerFallback,
                        poolCoordinator: this._poolCoordinator,
                        fetchFromNodeAsync: (peerNodeId, ct) => this._storageNodeClient.OpenImageStreamAsync(
                                storageNodeId: peerNodeId,
                                imageId: query.ImageId,
                                isPreview: true,
                                cancellationToken: ct),
                        cancellationToken: cancellationToken);
        }
}
