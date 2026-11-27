using ContentService.Application.Interfaces;
using ContentService.Application.Models;

namespace ContentService.Application.UseCases.Images;

/// <summary>
/// Lazily generates and persists lightweight preview images for legacy uploads that lack a stored preview.
/// </summary>
internal static class ImagePreviewBackfiller
{
        /// <summary>
        /// Reads the original binary, generates and saves a preview if possible, or falls back to the original stream.
        /// </summary>
        /// <param name="imageBinaryStore">The binary and preview store.</param>
        /// <param name="previewGenerator">Optional preview generator.</param>
        /// <param name="metadata">The image metadata projection.</param>
        /// <param name="cancellationToken">Propagates cancellation.</param>
        /// <returns>An <see cref="ImageStreamResult"/> carrying either the generated preview or the original image.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when the original binary payload is missing.</exception>
        public static async Task<ImageStreamResult> BackfillOrFallbackAsync(
                IImageBinaryStore imageBinaryStore,
                IImagePreviewGenerator? previewGenerator,
                ImageMetadataResult metadata,
                CancellationToken cancellationToken)
        {
                // Fetch the full-resolution original binary; if missing, the image record is orphaned.
                Stream? originalStream = await imageBinaryStore.OpenReadAsync(
                        imageId: metadata.Id,
                        cancellationToken: cancellationToken);
                if (originalStream is null)
                {
                        throw new KeyNotFoundException($"Binary payload for image '{metadata.Id}' was not found.");
                }

                // When no preview generator is registered, stream the original image directly.
                if (previewGenerator is null)
                {
                        return new ImageStreamResult(metadata: metadata, content: originalStream);
                }

                // Copy the original stream into memory and dispose the SQL stream before running image decoding.
                byte[] originalBytes;
                await using (originalStream)
                {
                        using MemoryStream copyBuffer = new();
                        await originalStream.CopyToAsync(destination: copyBuffer, cancellationToken: cancellationToken);
                        originalBytes = copyBuffer.ToArray();
                }

                // Attempt to generate a lightweight preview; fall back to the original bytes if decoding/resizing fails.
                ImagePreviewPayload? generated = await previewGenerator.TryGeneratePreviewAsync(
                        sourceBytes: originalBytes,
                        cancellationToken: cancellationToken);
                if (generated is null)
                {
                        return new ImageStreamResult(
                                metadata: metadata,
                                content: new MemoryStream(buffer: originalBytes, writable: false));
                }

                // Persist the newly generated preview so subsequent requests hit the fast path.
                await imageBinaryStore.SavePreviewAsync(
                        imageId: metadata.Id,
                        previewBytes: generated.Bytes,
                        contentType: generated.ContentType,
                        cancellationToken: cancellationToken);

                // Update ContentType and SizeBytes on the returned metadata to match the preview payload.
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

