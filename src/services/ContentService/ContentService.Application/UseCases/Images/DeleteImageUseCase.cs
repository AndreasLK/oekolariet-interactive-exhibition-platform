using ContentService.Application.Interfaces;
using ContentService.Application.Models;
using ContentService.Domain.Entities;
using ContentService.Domain.Exceptions;

namespace ContentService.Application.UseCases.Images;

/// <summary>
/// Soft-deletes an unreferenced exhibition image while enforcing referential integrity across templates.
/// </summary>
public sealed class DeleteImageUseCase
{
        /// <summary>
        /// Image metadata repository.
        /// </summary>
        private readonly IImageRepository _imageRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeleteImageUseCase"/> class.
        /// </summary>
        /// <param name="imageRepository">The image metadata repository.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="imageRepository"/> is <see langword="null"/>.</exception>
        public DeleteImageUseCase(IImageRepository imageRepository)
        {
                ArgumentNullException.ThrowIfNull(imageRepository);
                this._imageRepository = imageRepository;
        }

        /// <summary>
        /// Verifies the image is not referenced by any template and persists a soft-delete with audit and concurrency tracking.
        /// </summary>
        /// <param name="command">The delete command identifying the target image and acting user/service.</param>
        /// <param name="cancellationToken">A token to observe while checking references and saving state.</param>
        /// <returns>A task representing the asynchronous soft-delete operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="command"/> is <see langword="null"/>.</exception>
        /// <exception cref="DomainValidationException">Thrown when <see cref="DeleteImageCommand.ImageId"/> is empty or <see cref="DeleteImageCommand.ActorId"/> is invalid.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the image does not exist or is already soft-deleted.</exception>
        /// <exception cref="EntityInUseException">Thrown when any active or archived template still references the image.</exception>
        /// <exception cref="ConcurrencyConflictException">Thrown when the image was concurrently modified before deletion completed.</exception>
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

                ImageEntity? image = await this._imageRepository.GetByIdAsync(
                        id: command.ImageId,
                        cancellationToken: cancellationToken);
                // Treat already-soft-deleted images as missing so callers cannot distinguish deleted IDs from nonexistent ones.
                if (image is null || image.IsDeleted)
                {
                        throw new KeyNotFoundException($"Image '{command.ImageId}' was not found.");
                }

                bool isReferenced = await this._imageRepository.IsReferencedByAnyTemplateAsync(
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
}

