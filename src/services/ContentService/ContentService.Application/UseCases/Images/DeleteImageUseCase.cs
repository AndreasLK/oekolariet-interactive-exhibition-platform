using ContentService.Application.Common;
using ContentService.Application.Interfaces;
using ContentService.Application.Models;
using ContentService.Application.Options;
using ContentService.Domain.Entities;
using ContentService.Domain.Exceptions;
using Microsoft.Extensions.Options;

namespace ContentService.Application.UseCases.Images;

/// <summary>
/// Soft-deletes an unreferenced exhibition image while enforcing referential integrity across templates.
/// </summary>
public sealed class DeleteImageUseCase
{
        private readonly IImageRepository _imageRepository;
        private readonly IStorageNodeClient? _storageNodeClient;
        private readonly ContentStorageOptions? _options;
        private readonly IPoolCoordinator? _poolCoordinator;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeleteImageUseCase"/> class.
        /// </summary>
        /// <param name="imageRepository">The image metadata repository.</param>
        /// <param name="storageNodeClient">Optional peer storage node client.</param>
        /// <param name="options">Optional content storage options.</param>
        /// <param name="poolCoordinator">Optional stateless Dapr Pub/Sub pool coordinator.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="imageRepository"/> is <see langword="null"/>.</exception>
        public DeleteImageUseCase(
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
        /// Verifies the image is not referenced by any template and persists a soft-delete with audit and concurrency tracking.
        /// </summary>
        /// <param name="command">The delete command identifying the target image and acting user/service.</param>
        /// <param name="cancellationToken">A token to observe while checking references and saving state.</param>
        /// <returns>A task representing the asynchronous soft-delete operation.</returns>
        public async Task ExecuteAsync(DeleteImageCommand command, CancellationToken cancellationToken)
        {
                ArgumentNullException.ThrowIfNull(command);

                if (command.ImageId == Guid.Empty)
                {
                        throw new DomainValidationException("Image ID cannot be empty.");
                }

                if (string.IsNullOrWhiteSpace(command.ActorId))
                {
                        throw new DomainValidationException("Actor ID cannot be null or whitespace.");
                }

                if (this._storageNodeClient is not null
                        && StorageNodeTargetResolver.IsRemoteNode(
                                targetStorageNodeId: command.StorageNodeId,
                                localStorageNodeId: this._options?.StorageNodeId))
                {
                        await this._storageNodeClient.DeleteImageAsync(
                                storageNodeId: command.StorageNodeId!,
                                command: command,
                                cancellationToken: cancellationToken);
                        return;
                }

                ImageEntity? image = await this._imageRepository.GetByIdAsync(
                        id: command.ImageId,
                        cancellationToken: cancellationToken);
                // Treat already-soft-deleted images as missing locally; fall back to peer nodes for bare-GUID deletes.
                if (image is null || image.IsDeleted)
                {
                        bool deletedOnPeer = await this.TryDeleteOnPeerAsync(
                                command: command,
                                cancellationToken: cancellationToken);
                        if (deletedOnPeer)
                        {
                                return;
                        }

                        throw new KeyNotFoundException($"Image '{command.ImageId}' was not found.");
                }

                bool isReferenced = await ImageDeletionReferenceChecker.IsReferencedLocallyOrOnPeersAsync(
                        imageRepository: this._imageRepository,
                        storageNodeClient: this._storageNodeClient,
                        options: this._options,
                        imageStorageNodeId: image.StorageNodeId,
                        imageId: command.ImageId,
                        cancellationToken: cancellationToken);
                if (isReferenced)
                {
                        throw new EntityInUseException(
                                $"Image '{command.ImageId}' cannot be deleted because it is referenced by one or more templates.");
                }

                // Capture ConcurrencyStamp before MarkDeleted rotates it so the repository verifies the pre-mutation token.
                Guid originalConcurrencyStamp = image.ConcurrencyStamp;
                image.MarkDeleted(actorId: command.ActorId);

                await this._imageRepository.DeleteAsync(
                        image: image,
                        expectedConcurrencyStamp: originalConcurrencyStamp,
                        cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Probes peer nodes via Dapr to locate and delete a bare-GUID image absent locally.
        /// </summary>
        private async Task<bool> TryDeleteOnPeerAsync(
                DeleteImageCommand command,
                CancellationToken cancellationToken)
        {
                if (this._storageNodeClient is null || command.StorageNodeId is not null)
                {
                        return false;
                }

                string? owningNodeId = await StorageNodeTargetResolver.ResolveAcrossPoolAsync(
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
                                return existing is null ? null : peerNodeId;
                        },
                        cancellationToken: cancellationToken);

                if (owningNodeId is null)
                {
                        return false;
                }

                await this._storageNodeClient.DeleteImageAsync(
                        storageNodeId: owningNodeId,
                        command: command,
                        cancellationToken: cancellationToken);
                return true;
        }
}
