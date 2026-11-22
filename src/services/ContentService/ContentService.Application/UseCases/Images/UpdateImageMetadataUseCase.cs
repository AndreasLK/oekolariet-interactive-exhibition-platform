using ContentService.Application.Common;
using ContentService.Application.Interfaces;
using ContentService.Application.Models;
using ContentService.Domain.Entities;

namespace ContentService.Application.UseCases.Images;

/// <summary>
/// Updates searchable description (<c>AltText</c>), tags, and album path on an active image while enforcing optimistic concurrency.
/// </summary>
public sealed class UpdateImageMetadataUseCase
{
        private readonly IImageRepository _imageRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateImageMetadataUseCase"/> class.
        /// </summary>
        /// <param name="imageRepository">The image metadata repository.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="imageRepository"/> is <see langword="null"/>.</exception>
        public UpdateImageMetadataUseCase(IImageRepository imageRepository)
        {
                ArgumentNullException.ThrowIfNull(argument: imageRepository);
                this._imageRepository = imageRepository;
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

                ImageEntity? image = await this._imageRepository.GetByIdAsync(
                        id: command.ImageId,
                        cancellationToken: cancellationToken);

                if (image is null || image.IsDeleted)
                {
                        throw new KeyNotFoundException($"Image '{command.ImageId}' was not found.");
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
}

