using ContentService.Application.Interfaces;
using ContentService.Application.Common;
using ContentService.Application.Models;
using ContentService.Application.Options;
using ContentService.Domain.Entities;
using ContentService.Domain.Exceptions;
using Microsoft.Extensions.Options;

namespace ContentService.Application.UseCases.Images;

/// <summary>
/// Retrieves immutable metadata for a single active exhibition image.
/// </summary>
public sealed class GetImageMetadataUseCase
{
        /// <summary>
        /// Image metadata repository.
        /// </summary>
        private readonly IImageRepository _imageRepository;

        /// <summary>
        /// Optional peer storage node client used to forward requests for images stored on other nodes.
        /// </summary>
        private readonly IStorageNodeClient? _storageNodeClient;

        /// <summary>
        /// Optional content storage options supplying local and peer storage node IDs.
        /// </summary>
        private readonly ContentStorageOptions? _options;

        /// <summary>
        /// Optional stateless Dapr Pub/Sub pool coordinator.
        /// </summary>
        private readonly IPoolCoordinator? _poolCoordinator;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetImageMetadataUseCase"/> class.
        /// </summary>
        /// <param name="imageRepository">The image metadata repository.</param>
        /// <param name="storageNodeClient">Optional peer storage node client.</param>
        /// <param name="options">Optional content storage options.</param>
        /// <param name="poolCoordinator">Optional stateless Dapr Pub/Sub pool coordinator.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="imageRepository"/> is <see langword="null"/>.</exception>
        public GetImageMetadataUseCase(
                IImageRepository imageRepository,
                IStorageNodeClient? storageNodeClient = null,
                IOptions<ContentStorageOptions>? options = null,
                IPoolCoordinator? poolCoordinator = null)
        {
                ArgumentNullException.ThrowIfNull(imageRepository);
                this._imageRepository = imageRepository;
                this._storageNodeClient = storageNodeClient;
                this._options = options?.Value;
                this._poolCoordinator = poolCoordinator;
        }

        /// <summary>
        /// Fetches metadata for the active image identified by <paramref name="imageId"/>, forwarding to a peer node when <paramref name="storageNodeId"/> differs from the local node or when absent locally.
        /// </summary>
        /// <param name="imageId">The unique image identifier.</param>
        /// <param name="cancellationToken">A token to observe while querying metadata.</param>
        /// <param name="storageNodeId">Optional target storage node identifier.</param>
        /// <param name="allowPeerFallback">Whether to probe peer nodes if not found locally.</param>
        /// <returns>The <see cref="ImageMetadataResult"/> for the requested active image.</returns>
        /// <exception cref="DomainValidationException">Thrown when <paramref name="imageId"/> is <see cref="Guid.Empty"/>.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the image does not exist or has been soft-deleted.</exception>
        public async Task<ImageMetadataResult> ExecuteAsync(
                Guid imageId,
                CancellationToken cancellationToken,
                string? storageNodeId = null,
                bool allowPeerFallback = true)
        {
                if (imageId == Guid.Empty)
                {
                        throw new DomainValidationException("Image ID cannot be empty.");
                }

                if (this._storageNodeClient is not null
                        && StorageNodeTargetResolver.IsRemoteNode(
                                targetStorageNodeId: storageNodeId,
                                localStorageNodeId: this._options?.StorageNodeId))
                {
                        ImageMetadataResult? remoteMetadata = await this._storageNodeClient.GetImageMetadataAsync(
                                storageNodeId: storageNodeId!,
                                imageId: imageId,
                                cancellationToken: cancellationToken);

                        return remoteMetadata
                                ?? throw new KeyNotFoundException($"Image '{storageNodeId}_{imageId}' was not found.");
                }

                ImageEntity? image = await this._imageRepository.GetByIdAsync(
                        id: imageId,
                        cancellationToken: cancellationToken);

                // Treat soft-deleted images identically to missing records so callers cannot probe deleted references.
                if (image is not null && !image.IsDeleted)
                {
                        return ContentResultMapper.ToMetadataResult(image: image);
                }

                if (this._storageNodeClient is not null && storageNodeId is null)
                {
                        ImageMetadataResult? peerMetadata = await StorageNodeTargetResolver.ResolveAcrossPoolAsync(
                                imageId: imageId,
                                options: this._options,
                                allowPeerFallback: allowPeerFallback,
                                poolCoordinator: this._poolCoordinator,
                                fetchFromNodeAsync: (peerNodeId, ct) => this._storageNodeClient.GetImageMetadataAsync(
                                        storageNodeId: peerNodeId,
                                        imageId: imageId,
                                        cancellationToken: ct),
                                cancellationToken: cancellationToken);

                        if (peerMetadata is not null)
                        {
                                return peerMetadata;
                        }
                }

                throw new KeyNotFoundException($"Image '{imageId}' was not found.");
        }
}

