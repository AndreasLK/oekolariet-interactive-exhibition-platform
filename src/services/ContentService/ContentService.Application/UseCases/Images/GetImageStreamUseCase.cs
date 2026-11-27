using ContentService.Application.Common;
using ContentService.Application.Interfaces;
using ContentService.Application.Models;
using ContentService.Application.Options;
using ContentService.Domain.Entities;
using ContentService.Domain.Exceptions;
using Microsoft.Extensions.Options;

namespace ContentService.Application.UseCases.Images;

/// <summary>
/// Loads an active exhibition image's metadata and binary stream on demand.
/// </summary>
public sealed class GetImageStreamUseCase
{
        /// <summary>
        /// Provides image metadata and checks whether the image has been soft-deleted.
        /// </summary>
        private readonly IImageRepository _imageRepository;

        /// <summary>
        /// Loads stored image bytes separately from metadata queries.
        /// </summary>
        private readonly IImageBinaryStore _imageBinaryStore;

        /// <summary>
        /// Optional peer storage node client used to stream images stored on other nodes.
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
        /// Captures metadata and binary store dependencies so image downloads stay decoupled from SQL Server details.
        /// </summary>
        /// <param name="imageRepository">The image metadata repository.</param>
        /// <param name="imageBinaryStore">The binary stream store.</param>
        /// <param name="storageNodeClient">Optional peer storage node client.</param>
        /// <param name="options">Optional content storage options.</param>
        /// <param name="poolCoordinator">Optional stateless Dapr Pub/Sub pool coordinator.</param>
        /// <exception cref="ArgumentNullException">Thrown when any required dependency is <see langword="null"/>.</exception>
        public GetImageStreamUseCase(
                IImageRepository imageRepository,
                IImageBinaryStore imageBinaryStore,
                IStorageNodeClient? storageNodeClient = null,
                IOptions<ContentStorageOptions>? options = null,
                IPoolCoordinator? poolCoordinator = null)
        {
                ArgumentNullException.ThrowIfNull(imageRepository);
                ArgumentNullException.ThrowIfNull(imageBinaryStore);

                this._imageRepository = imageRepository;
                this._imageBinaryStore = imageBinaryStore;
                this._storageNodeClient = storageNodeClient;
                this._options = options?.Value;
                this._poolCoordinator = poolCoordinator;
        }

        /// <summary>
        /// Verifies the image exists and is not soft-deleted, then returns its metadata and readable byte stream.
        /// </summary>
        /// <param name="query">The image stream query containing the image ID.</param>
        /// <param name="cancellationToken">Propagates caller cancellation while loading metadata or image bytes.</param>
        /// <returns>An <see cref="ImageStreamResult"/> carrying metadata and an open stream whose disposal ownership transfers to the caller.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
        /// <exception cref="DomainValidationException">Thrown when <see cref="GetImageStreamQuery.ImageId"/> is <see cref="Guid.Empty"/>.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the image is missing, soft-deleted, or its binary payload cannot be found.</exception>
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
                        ImageStreamResult? remoteStream = await this._storageNodeClient.OpenImageStreamAsync(
                                storageNodeId: query.StorageNodeId!,
                                imageId: query.ImageId,
                                isPreview: false,
                                cancellationToken: cancellationToken);

                        return remoteStream
                                ?? throw new KeyNotFoundException($"Image '{query.StorageNodeId}_{query.ImageId}' was not found.");
                }

                ImageEntity? image = await this._imageRepository.GetByIdAsync(
                        id: query.ImageId,
                        cancellationToken: cancellationToken);
                // Treat soft-deleted images as missing; fall back to peer nodes for bare-GUID lookups.
                if (image is null || image.IsDeleted)
                {
                        ImageStreamResult? peerStream = await this.TryOpenPeerStreamAsync(
                                query: query,
                                cancellationToken: cancellationToken);

                        return peerStream
                                ?? throw new KeyNotFoundException($"Image '{query.ImageId}' was not found.");
                }

                ImageMetadataResult metadata = ContentResultMapper.ToMetadataResult(image: image);
                Stream? contentStream = await this._imageBinaryStore.OpenReadAsync(
                        imageId: image.Id,
                        cancellationToken: cancellationToken);
                if (contentStream is null)
                {
                        throw new KeyNotFoundException($"Binary payload for image '{query.ImageId}' was not found.");
                }

                // Dispose the opened stream immediately if constructing the result throws.
                try
                {
                        return new ImageStreamResult(metadata: metadata, content: contentStream);
                }
                catch
                {
                        await contentStream.DisposeAsync();
                        throw;
                }
        }

        /// <summary>
        /// Probes peer nodes via Dapr when a bare-GUID image is absent locally.
        /// </summary>
        private Task<ImageStreamResult?> TryOpenPeerStreamAsync(
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
                                isPreview: false,
                                cancellationToken: ct),
                        cancellationToken: cancellationToken);
        }
}
