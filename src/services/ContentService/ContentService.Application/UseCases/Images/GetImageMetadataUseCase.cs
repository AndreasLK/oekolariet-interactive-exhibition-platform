using ContentService.Application.Interfaces;
using ContentService.Application.Common;
using ContentService.Application.Models;
using ContentService.Domain.Entities;
using ContentService.Domain.Exceptions;

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
        /// Initializes a new instance of the <see cref="GetImageMetadataUseCase"/> class.
        /// </summary>
        /// <param name="imageRepository">The image metadata repository.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="imageRepository"/> is <see langword="null"/>.</exception>
        public GetImageMetadataUseCase(IImageRepository imageRepository)
        {
                ArgumentNullException.ThrowIfNull(imageRepository);
                this._imageRepository = imageRepository;
        }

        /// <summary>
        /// Fetches metadata for the active image identified by <paramref name="imageId"/>.
        /// </summary>
        /// <param name="imageId">The unique image identifier.</param>
        /// <param name="cancellationToken">A token to observe while querying metadata.</param>
        /// <returns>The <see cref="ImageMetadataResult"/> for the requested active image.</returns>
        /// <exception cref="DomainValidationException">Thrown when <paramref name="imageId"/> is <see cref="Guid.Empty"/>.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the image does not exist or has been soft-deleted.</exception>
        public async Task<ImageMetadataResult> ExecuteAsync(Guid imageId, CancellationToken cancellationToken)
        {
                if (imageId == Guid.Empty)
                {
                        throw new DomainValidationException("Image ID cannot be empty.");
                }

                ImageEntity? image = await this._imageRepository.GetByIdAsync(
                        id: imageId,
                        cancellationToken: cancellationToken);

                // Treat soft-deleted images identically to missing records so callers cannot probe deleted references.
                if (image is null || image.IsDeleted)
                {
                        throw new KeyNotFoundException($"Image '{imageId}' was not found.");
                }

                return ContentResultMapper.ToMetadataResult(image: image);
        }
}

