using ContentService.Application.Common;
using ContentService.Application.Interfaces;
using ContentService.Application.Models;
using ContentService.Application.Options;
using ContentService.Domain.Entities;
using Microsoft.Extensions.Options;

namespace ContentService.Application.UseCases.Images;

/// <summary>
/// Updates searchable description (<c>AltText</c>), tags, and album path on an active image while enforcing optimistic concurrency.
/// </summary>
public sealed class UpdateImageMetadataUseCase
{
        private readonly IImageRepository _imageRepository;
        private readonly IStorageNodeClient? _storageNodeClient;
        private readonly ContentStorageOptions? _options;
        private readonly IPoolCoordinator? _poolCoordinator;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateImageMetadataUseCase"/> class.
        /// </summary>
        /// <param name="imageRepository">The image metadata repository.</param>
        /// <param name="storageNodeClient">Optional peer storage node client.</param>
        /// <param name="options">Optional content storage options.</param>
        /// <param name="poolCoordinator">Optional stateless Dapr Pub/Sub pool coordinator.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="imageRepository"/> is <see langword="null"/>.</exception>
        public UpdateImageMetadataUseCase(
                IImageRepository imageRepository,
                IStorageNodeClient? storageNodeClient = null,
                IOptions<ContentStorageOptions>? options = null,
                IPoolCoordinator? poolCoordinator = null)
        {
                ArgumentNullException.ThrowIfNull(argument: imageRepository);
                this._imageRepository = imageRepository;
                this._storageNodeClient = storageNodeClient;
                this._options = options?.Value;
                this._poolCoordinator = poolCoordinator;
        }

        /// <summary>
        /// Validates and persists metadata edits for an active image, returning the updated projection with a rotated concurrency stamp.
        /// </summary>
        /// <param name="command">The metadata update command including expected concurrency stamp and actor ID.</param>
        /// <param name="cancellationToken">A token to observe while loading and updating the image.</param>
        /// <returns>The updated <see cref="ImageMetadataResult"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="command"/> is <see langword="null"/>.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the image does not exist or has been soft-deleted.</exception>
        public async Task<ImageMetadataResult> ExecuteAsync(
                UpdateImageMetadataCommand command,
                CancellationToken cancellationToken)
        {
                ArgumentNullException.ThrowIfNull(argument: command);

                if (this._storageNodeClient is not null
                        && StorageNodeTargetResolver.IsRemoteNode(
                                targetStorageNodeId: command.StorageNodeId,
                                localStorageNodeId: this._options?.StorageNodeId))
                {
                        return await this._storageNodeClient.UpdateImageMetadataAsync(
                                storageNodeId: command.StorageNodeId!,
                                command: command,
                                cancellationToken: cancellationToken);
                }

                ImageEntity? image = await this._imageRepository.GetByIdAsync(
                        id: command.ImageId,
                        cancellationToken: cancellationToken);

                if (image is null || image.IsDeleted)
                {
                        ImageMetadataResult? peerUpdated = await this.TryUpdateOnPeerAsync(
                                command: command,
                                cancellationToken: cancellationToken);

                        return peerUpdated
                                ?? throw new KeyNotFoundException($"Image '{command.ImageId}' was not found.");
                }

                // Mutate through the domain entity so alt-text, tags, album-path, and audit invariants run before persistence.
                image.UpdateMetadata(
                        altText: command.AltText,
                        tags: command.Tags,
                        albumPath: command.AlbumPath,
                        actorId: command.ActorId);

                await this._imageRepository.UpdateMetadataAsync(
                        image: image,
                        expectedConcurrencyStamp: command.ExpectedConcurrencyStamp,
                        cancellationToken: cancellationToken);

                return ContentResultMapper.ToMetadataResult(image: image);
        }

        /// <summary>
        /// Probes peer nodes via Dapr to locate and update a bare-GUID image absent locally.
        /// </summary>
        private Task<ImageMetadataResult?> TryUpdateOnPeerAsync(
                UpdateImageMetadataCommand command,
                CancellationToken cancellationToken)
        {
                if (this._storageNodeClient is null || command.StorageNodeId is not null)
                {
                        return Task.FromResult<ImageMetadataResult?>(null);
                }

                return StorageNodeTargetResolver.ResolveAcrossPoolAsync(
                        imageId: command.ImageId,
                        options: this._options,
                        allowPeerFallback: command.AllowPeerFallback,
                        poolCoordinator: this._poolCoordinator,
                        fetchFromNodeAsync: async (peerNodeId, ct) =>
                        {
                                ImageMetadataResult? existing = await this._storageNodeClient.GetImageMetadataAsync(
                                        storageNodeId: peerNodeId,
                                        imageId: command.ImageId,
                                        cancellationToken: ct);
                                if (existing is null)
                                {
                                        return null;
                                }

                                return await this._storageNodeClient.UpdateImageMetadataAsync(
                                        storageNodeId: peerNodeId,
                                        command: command,
                                        cancellationToken: ct);
                        },
                        cancellationToken: cancellationToken);
        }
}

