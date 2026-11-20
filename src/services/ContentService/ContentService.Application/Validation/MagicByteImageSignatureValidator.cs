using ContentService.Application.Interfaces;
using ContentService.Domain.Common;

namespace ContentService.Application.Validation;

/// <summary>
/// Validates uploaded image payloads by inspecting leading binary magic bytes against the declared raster MIME content type.
/// </summary>
/// <remarks>
/// Rejects spoofed file extensions and active-content payloads (e.g., SVG or HTML renamed as PNG) before persisting binaries.
/// </remarks>
public sealed class MagicByteImageSignatureValidator : IImageSignatureValidator
{
        /// <summary>
        /// Standard 8-byte PNG file signature (<c>89 50 4E 47 0D 0A 1A 0A</c>).
        /// </summary>
        private static readonly byte[] PNG_SIGNATURE = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        /// <summary>
        /// Standard 3-byte JPEG Start-Of-Image + marker prefix (<c>FF D8 FF</c>).
        /// </summary>
        private static readonly byte[] JPEG_SIGNATURE = [0xFF, 0xD8, 0xFF];

        /// <summary>
        /// ASCII <c>RIFF</c> container signature required at offset 0 of WebP images (<c>52 49 46 46</c>).
        /// </summary>
        private static readonly byte[] WEBP_RIFF_SIGNATURE = [0x52, 0x49, 0x46, 0x46];

        /// <summary>
        /// ASCII <c>WEBP</c> chunk signature required at offset 8 of WebP images (<c>57 45 42 50</c>).
        /// </summary>
        private static readonly byte[] WEBP_FORMAT_SIGNATURE = [0x57, 0x45, 0x42, 0x50];

        /// <summary>
        /// Byte offset at which the <c>WEBP</c> fourCC identifier begins inside a RIFF header.
        /// </summary>
        private const int WEBP_FORMAT_OFFSET = 8;

        /// <summary>
        /// Minimum byte count required to inspect both <c>RIFF</c> and <c>WEBP</c> headers in a WebP payload.
        /// </summary>
        private const int WEBP_MINIMUM_LENGTH = 12;

        /// <summary>
        /// Inspects leading payload bytes to verify they match the expected magic-byte signature for <paramref name="declaredContentType"/>.
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

                // Exact canonical matching after trim+lowercase rejects parameterized headers (e.g., "image/png; charset=utf-8") and SVG/HTML consistently with DomainConstants.
                string normalizedContentType = declaredContentType.Trim().ToLowerInvariant();
                return normalizedContentType switch
                {
                        DomainConstants.CONTENT_TYPE_PNG => headerBytes.StartsWith(PNG_SIGNATURE),
                        DomainConstants.CONTENT_TYPE_JPEG => headerBytes.StartsWith(JPEG_SIGNATURE),
                        DomainConstants.CONTENT_TYPE_WEBP => IsWebpSignature(headerBytes: headerBytes),
                        _ => false
                };
        }

        /// <summary>
        /// Verifies the two-part RIFF/WEBP container header required by the WebP specification.
        /// </summary>
        /// <param name="headerBytes">The leading bytes of the payload.</param>
        /// <returns><see langword="true"/> if both <c>RIFF</c> (offset 0) and <c>WEBP</c> (offset 8) markers match; otherwise <see langword="false"/>.</returns>
        private static bool IsWebpSignature(ReadOnlySpan<byte> headerBytes)
        {
                // Guard minimum length before slicing offset 8..12 so truncated payloads return false rather than throwing ArgumentOutOfRangeException.
                if (headerBytes.Length < WEBP_MINIMUM_LENGTH)
                {
                        return false;
                }

                return headerBytes.StartsWith(WEBP_RIFF_SIGNATURE)
                        && headerBytes.Slice(start: WEBP_FORMAT_OFFSET, length: WEBP_FORMAT_SIGNATURE.Length)
                                .SequenceEqual(WEBP_FORMAT_SIGNATURE);
        }
}

