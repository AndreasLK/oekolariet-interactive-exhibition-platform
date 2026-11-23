using ContentService.Application.Interfaces;
using SkiaSharp;

namespace ContentService.Infrastructure.Images;

/// <summary>
/// Generates compact WebP gallery previews using SkiaSharp so gallery grids never download multi-megabyte originals.
/// </summary>
public sealed class SkiaImagePreviewGenerator : IImagePreviewGenerator
{
        /// <summary>
        /// Maximum width or height in pixels for generated gallery preview thumbnails.
        /// </summary>
        private const int MAX_PREVIEW_DIMENSION_PX = 400;

        /// <summary>
        /// WebP encoder quality balancing crisp gallery previews with tiny payloads.
        /// </summary>
        private const int PREVIEW_WEBP_QUALITY = 80;

        /// <summary>
        /// MIME content type emitted for generated WebP preview payloads.
        /// </summary>
        private const string PREVIEW_CONTENT_TYPE = "image/webp";

        /// <inheritdoc />
        public Task<ImagePreviewPayload?> TryGeneratePreviewAsync(
                byte[] sourceBytes,
                CancellationToken cancellationToken)
        {
                if (sourceBytes is null || sourceBytes.Length == 0)
                {
                        return Task.FromResult<ImagePreviewPayload?>(result: null);
                }

                cancellationToken.ThrowIfCancellationRequested();

                using SKBitmap? sourceBitmap = SKBitmap.Decode(buffer: sourceBytes);
                if (sourceBitmap is null || sourceBitmap.Width <= 0 || sourceBitmap.Height <= 0)
                {
                        // Unsupported or synthetic stub bytes fall back to original bytes cleanly.
                        return Task.FromResult<ImagePreviewPayload?>(result: null);
                }

                (int targetWidth, int targetHeight) = ComputeBoundedDimensions(
                        width: sourceBitmap.Width,
                        height: sourceBitmap.Height);

                using SKBitmap resizedBitmap = targetWidth == sourceBitmap.Width && targetHeight == sourceBitmap.Height
                        ? sourceBitmap
                        : sourceBitmap.Resize(
                                info: new SKImageInfo(width: targetWidth, height: targetHeight),
                                sampling: new SKSamplingOptions(filter: SKFilterMode.Linear, mipmap: SKMipmapMode.Linear));

                if (resizedBitmap is null)
                {
                        return Task.FromResult<ImagePreviewPayload?>(result: null);
                }

                using SKImage encodedImage = SKImage.FromBitmap(bitmap: resizedBitmap);
                using SKData? webpData = encodedImage.Encode(
                        format: SKEncodedImageFormat.Webp,
                        quality: PREVIEW_WEBP_QUALITY);

                if (webpData is null)
                {
                        return Task.FromResult<ImagePreviewPayload?>(result: null);
                }

                return Task.FromResult<ImagePreviewPayload?>(result: new ImagePreviewPayload(
                        Bytes: webpData.ToArray(),
                        ContentType: PREVIEW_CONTENT_TYPE));
        }

        private static (int Width, int Height) ComputeBoundedDimensions(int width, int height)
        {
                if (width <= MAX_PREVIEW_DIMENSION_PX && height <= MAX_PREVIEW_DIMENSION_PX)
                {
                        return (width, height);
                }

                double scale = Math.Min(
                        val1: (double)MAX_PREVIEW_DIMENSION_PX / width,
                        val2: (double)MAX_PREVIEW_DIMENSION_PX / height);
                int scaledWidth = Math.Max(val1: 1, val2: (int)Math.Round(a: width * scale));
                int scaledHeight = Math.Max(val1: 1, val2: (int)Math.Round(a: height * scale));
                return (scaledWidth, scaledHeight);
        }
}

