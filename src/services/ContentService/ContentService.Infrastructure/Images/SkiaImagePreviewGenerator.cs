using ContentService.Application.Interfaces;
using SkiaSharp;

namespace ContentService.Infrastructure.Images;

/// <summary>
/// Generates compact WebP gallery previews using SkiaSharp so gallery grids never download multi-megabyte originals.
/// </summary>
/// <remarks>
/// Non-standard decode pipeline: Instead of calling <c>SKBitmap.Decode(byte[])</c> directly, we open an
/// <see cref="SKCodec"/> first and enforce dimension, pixel-count, and uncompressed byte-size caps before decoding.
/// <para>
/// What we gain: Protection against decompression-bomb OOM/DoS attacks, avoidance of SkiaSharp 3.x
/// <c>ArgumentNullException</c> on synthetic/corrupt headers, and tiny 400px WebP thumbnails.
/// What we lose: Slight CPU/memory cost during upload and lossy WebP compression (quality 80) on previews.
/// </para>
/// </remarks>
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

        /// <summary>
        /// Maximum total pixel count allowed before decoding to block decompression bombs (~100 MB at 4 bytes per pixel).
        /// </summary>
        private const long MAX_DECODE_PIXEL_COUNT = 25_000_000L;

        /// <summary>
        /// Maximum single-axis dimension in pixels allowed before decoding to prevent oversized scanline allocations.
        /// </summary>
        private const int MAX_DECODE_DIMENSION_PX = 16_384;

        /// <summary>
        /// Maximum uncompressed bitmap byte size allowed before decoding to bound HDR/high-bit-depth formats.
        /// </summary>
        private const long MAX_DECODE_BYTE_COUNT = 100_000_000L;

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

                // Create the codec explicitly because SKBitmap.Decode(byte[]) throws ArgumentNullException when synthetic or truncated headers yield a null SKCodec in SkiaSharp 3.x.
                using MemoryStream sourceStream = new(buffer: sourceBytes, writable: false);
                using SKCodec? codec = SKCodec.Create(stream: sourceStream);
                if (codec is null)
                {
                        // Unsupported or synthetic stub bytes fall back to original bytes cleanly.
                        return Task.FromResult<ImagePreviewPayload?>(result: null);
                }

                // Validate header dimensions and byte size before decoding to block decompression-bomb allocations.
                if (!IsDecodableImageSize(sourceInfo: codec.Info))
                {
                        return Task.FromResult<ImagePreviewPayload?>(result: null);
                }

                using SKBitmap? sourceBitmap = SKBitmap.Decode(codec: codec);
                if (sourceBitmap is null)
                {
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

        /// <summary>
        /// Validates codec header dimensions and uncompressed byte size before allocating pixel memory.
        /// </summary>
        /// <param name="sourceInfo">Image metadata read from the codec header.</param>
        /// <returns><c>true</c> when the image is safe to decode; otherwise, <c>false</c>.</returns>
        private static bool IsDecodableImageSize(SKImageInfo sourceInfo)
        {
                if (sourceInfo.Width <= 0 || sourceInfo.Height <= 0)
                {
                        return false;
                }

                if (sourceInfo.Width > MAX_DECODE_DIMENSION_PX || sourceInfo.Height > MAX_DECODE_DIMENSION_PX)
                {
                        return false;
                }

                // Cast to long so large dimensions (e.g., 50,000 x 50,000) cannot overflow Int32 into a negative product.
                long pixelCount = (long)sourceInfo.Width * sourceInfo.Height;
                if (pixelCount > MAX_DECODE_PIXEL_COUNT)
                {
                        return false;
                }

                // Guard BytesSize64 as well so high-bit-depth formats (e.g., RgbaF16/RgbaF32) cannot exceed the memory cap.
                return sourceInfo.BytesSize64 > 0 && sourceInfo.BytesSize64 <= MAX_DECODE_BYTE_COUNT;
        }

        /// <summary>
        /// Computes aspect-preserving preview dimensions bounded by <see cref="MAX_PREVIEW_DIMENSION_PX"/>.
        /// </summary>
        /// <param name="width">Original image width in pixels.</param>
        /// <param name="height">Original image height in pixels.</param>
        /// <returns>The bounded target width and height, each at least 1 pixel.</returns>
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

