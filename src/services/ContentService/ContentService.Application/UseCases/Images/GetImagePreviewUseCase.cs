using ContentService.Application.Common;
using ContentService.Application.Interfaces;
using ContentService.Application.Models;
using ContentService.Domain.Entities;
using ContentService.Domain.Exceptions;

namespace ContentService.Application.UseCases.Images;

/// <summary>
/// Serves a saved lightweight preview stream for an active image, generating and persisting it on first access if absent.
/// </summary>
public sealed class GetImagePreviewUseCase
{
        /// <summary>
        /// Provides image metadata and verifies whether the image has been soft-deleted.
        /// </summary>
        private readonly IImageRepository _imageRepository;

        /// <summary>
        /// Reads and persists image binary and preview payloads.
        /// </summary>
        private readonly IImageBinaryStore _imageBinaryStore;

        /// <summary>
        /// Optional preview generator used to backfill previews for images uploaded before preview persistence existed.
        /// </summary>
        private readonly IImagePreviewGenerator? _previewGenerator;

        /// <summary>
        /// Captures repository, binary store, and preview generator dependencies.
        /// </summary>
        /// <param name="imageRepository">The image metadata repository.</param>
        /// <param name="imageBinaryStore">The binary and preview stream store.</param>
        /// <param name="previewGenerator">Optional preview generator for lazy backfill.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="imageRepository"/> or <paramref name="imageBinaryStore"/> is <see langword="null"/>.</exception>
        public GetImagePreviewUseCase(
                IImageRepository imageRepository,
                IImageBinaryStore imageBinaryStore,
                IImagePreviewGenerator? previewGenerator = null)
        {
                ArgumentNullException.ThrowIfNull(imageRepository);
                ArgumentNullException.ThrowIfNull(imageBinaryStore);

                this._imageRepository = imageRepository;
                this._imageBinaryStore = imageBinaryStore;
                this._previewGenerator = previewGenerator;
        }

        /// <summary>
        /// Returns the saved preview stream for <paramref name="query"/>, generating and saving it on first request if missing.
        /// </summary>
        /// <param name="query">The image query identifying the target image.</param>
        /// <param name="cancellationToken">A token to cancel database reads or preview generation.</param>
        /// <returns>An <see cref="ImageStreamResult"/> carrying the preview stream and effective MIME type.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
        /// <exception cref="DomainValidationException">Thrown when <see cref="GetImageStreamQuery.ImageId"/> is <see cref="Guid.Empty"/>.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the image is missing or soft-deleted.</exception>
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
                if (image is null || image.IsDeleted)
                {
                        throw new KeyNotFoundException($"Image '{query.ImageId}' was not found.");
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

                // Backfill path: read the original once, generate and persist the preview, or fall back to original bytes.
                Stream? originalStream = await this._imageBinaryStore.OpenReadAsync(
                        imageId: image.Id,
                        cancellationToken: cancellationToken);
                if (originalStream is null)
                {
                        throw new KeyNotFoundException($"Binary payload for image '{query.ImageId}' was not found.");
                }

                if (this._previewGenerator is null)
                {
                        return new ImageStreamResult(metadata: metadata, content: originalStream);
                }

                byte[] originalBytes;
                await using (originalStream)
                {
                        using MemoryStream copyBuffer = new();
                        await originalStream.CopyToAsync(destination: copyBuffer, cancellationToken: cancellationToken);
                        originalBytes = copyBuffer.ToArray();
                }

                ImagePreviewPayload? generated = await this._previewGenerator.TryGeneratePreviewAsync(
                        sourceBytes: originalBytes,
                        cancellationToken: cancellationToken);
                if (generated is null)
                {
                        return new ImageStreamResult(
                                metadata: metadata,
                                content: new MemoryStream(buffer: originalBytes, writable: false));
                }

                await this._imageBinaryStore.SavePreviewAsync(
                        imageId: image.Id,
                        previewBytes: generated.Bytes,
                        contentType: generated.ContentType,
                        cancellationToken: cancellationToken);

                ImageMetadataResult generatedMetadata = metadata with
                {
                        ContentType = generated.ContentType,
                        SizeBytes = generated.Bytes.Length
                };
                return new ImageStreamResult(
                        metadata: generatedMetadata,
                        content: new MemoryStream(buffer: generated.Bytes, writable: false));
        }
}
