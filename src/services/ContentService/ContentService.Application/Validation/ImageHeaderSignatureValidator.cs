using ContentService.Application.Interfaces;
using ContentService.Domain.Common;

namespace ContentService.Application.Validation;

/// <summary>
/// Validates uploaded images by checking the fixed format header bytes at the start of the file against the declared MIME type.
/// </summary>
/// <remarks>
/// Catches renamed or disguised files (such as an SVG or HTML script renamed to <c>.png</c>) before saving them to SQL Server.
/// </remarks>
public sealed class ImageHeaderSignatureValidator : IImageSignatureValidator
{
        /// <summary>
        /// Every valid PNG file starts with these exact 8 bytes (<c>89 50 4E 47 0D 0A 1A 0A</c>).
        /// </summary>
        private static readonly byte[] PNG_HEADER_SIGNATURE = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        /// <summary>
        /// Every valid JPEG file starts with these exact 3 bytes (<c>FF D8 FF</c>).
        /// </summary>
        private static readonly byte[] JPEG_HEADER_SIGNATURE = [0xFF, 0xD8, 0xFF];

        /// <summary>
        /// Every WebP file starts with the 4 ASCII letters <c>RIFF</c> at byte offset 0 (<c>52 49 46 46</c>).
        /// </summary>
        private static readonly byte[] WEBP_RIFF_SIGNATURE = [0x52, 0x49, 0x46, 0x46];

        /// <summary>
        /// WebP files also contain the 4 ASCII letters <c>WEBP</c> at byte offset 8 (<c>57 45 42 50</c>).
        /// </summary>
        private static readonly byte[] WEBP_FORMAT_SIGNATURE = [0x57, 0x45, 0x42, 0x50];

        /// <summary>
        /// Byte index where the <c>WEBP</c> marker begins inside a RIFF container header.
        /// </summary>
        private const int WEBP_FORMAT_OFFSET = 8;

        /// <summary>
        /// Minimum number of leading bytes required to check PNG (8), JPEG (3), and WebP (12) format headers.
        /// </summary>
        private const int WEBP_MINIMUM_HEADER_BYTES = 12;

        /// <inheritdoc />
        public int RequiredHeaderByteCount => WEBP_MINIMUM_HEADER_BYTES;

        /// <summary>
        /// Inspects leading file bytes to verify they match the expected format header for <paramref name="declaredContentType"/>.
        /// </summary>
        /// <param name="headerBytes">The leading bytes read from the uploaded image payload.</param>
        /// <param name="declaredContentType">The MIME content type declared by the caller.</param>
        /// <returns><see langword="true"/> if <paramref name="headerBytes"/> matches the declared raster format; otherwise <see langword="false"/>.</returns>
        public bool IsValidSignature(ReadOnlySpan<byte> headerBytes, string declaredContentType)
        {
                if (headerBytes.IsEmpty || string.IsNullOrWhiteSpace(declaredContentType))
                {
                        return false;
                }

                // Exact matching after trim+lowercase rejects parameterized headers (e.g., "image/png; charset=utf-8") and SVG/HTML consistently with DomainConstants.
                string normalizedContentType = declaredContentType.Trim().ToLowerInvariant();
                return normalizedContentType switch
                {
                        DomainConstants.CONTENT_TYPE_PNG => headerBytes.StartsWith(PNG_HEADER_SIGNATURE),
                        DomainConstants.CONTENT_TYPE_JPEG => headerBytes.StartsWith(JPEG_HEADER_SIGNATURE),
                        DomainConstants.CONTENT_TYPE_WEBP => IsWebpHeader(headerBytes: headerBytes),
                        _ => false
                };
        }

        /// <summary>
        /// Verifies the two-part <c>RIFF</c> + <c>WEBP</c> container header required by the WebP format.
        /// </summary>
        /// <param name="headerBytes">The leading bytes of the payload.</param>
        /// <returns><see langword="true"/> if both <c>RIFF</c> (offset 0) and <c>WEBP</c> (offset 8) markers match; otherwise <see langword="false"/>.</returns>
        private static bool IsWebpHeader(ReadOnlySpan<byte> headerBytes)
        {
                // Guard minimum length before slicing bytes 8..12 so short payloads return false instead of throwing ArgumentOutOfRangeException.
                if (headerBytes.Length < WEBP_MINIMUM_HEADER_BYTES)
                {
                        return false;
                }

                return headerBytes.StartsWith(WEBP_RIFF_SIGNATURE)
                        && headerBytes.Slice(start: WEBP_FORMAT_OFFSET, length: WEBP_FORMAT_SIGNATURE.Length)
                                .SequenceEqual(WEBP_FORMAT_SIGNATURE);
        }
}

