using ContentService.Application.Common;
using ContentService.Application.Interfaces;
using ContentService.Application.Models;
using ContentService.Domain.Entities;
using ContentService.Domain.Exceptions;

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
        /// Captures metadata and binary store dependencies so image downloads stay decoupled from SQL Server details.
        /// </summary>
        /// <param name="imageRepository">The image metadata repository.</param>
        /// <param name="imageBinaryStore">The binary stream store.</param>
        /// <exception cref="ArgumentNullException">Thrown when any required dependency is <see langword="null"/>.</exception>
        public GetImageStreamUseCase(
                IImageRepository imageRepository,
                IImageBinaryStore imageBinaryStore)
        {
                ArgumentNullException.ThrowIfNull(imageRepository);
                ArgumentNullException.ThrowIfNull(imageBinaryStore);

                this._imageRepository = imageRepository;
                this._imageBinaryStore = imageBinaryStore;
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

                ImageEntity? image = await this._imageRepository.GetByIdAsync(
                        id: query.ImageId,
                        cancellationToken: cancellationToken);
                // Treat soft-deleted images as missing so deleted assets cannot be downloaded.
                if (image is null || image.IsDeleted)
                {
                        throw new KeyNotFoundException($"Image '{query.ImageId}' was not found.");
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
}
