using System.Text;
using ContentService.Application.Validation;
using ContentService.Domain.Common;

namespace ContentService.Tests.Application;

/// <summary>
/// Unit tests for <see cref="MagicByteImageSignatureValidator"/> verifying binary header allowlists and spoofing rejection.
/// </summary>
public sealed class MagicByteImageSignatureValidatorTests
{
        /// <summary>
        /// Valid 8-byte PNG signature followed by padding bytes.
        /// </summary>
        private static readonly byte[] VALID_PNG_BYTES = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];

        /// <summary>
        /// Valid JPEG SOI + marker header bytes.
        /// </summary>
        private static readonly byte[] VALID_JPEG_BYTES = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];

        /// <summary>
        /// Valid RIFF/WEBP container header bytes.
        /// </summary>
        private static readonly byte[] VALID_WEBP_BYTES = [0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50, 0x56, 0x50];

        /// <summary>
        /// Incomplete PNG magic header missing its final signature byte.
        /// </summary>
        private static readonly byte[] TRUNCATED_PNG_BYTES = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A];

        /// <summary>
        /// Incomplete JPEG header lacking the third marker byte.
        /// </summary>
        private static readonly byte[] TRUNCATED_JPEG_BYTES = [0xFF, 0xD8];

        /// <summary>
        /// Incomplete WebP header missing the final 'P' in the WEBP chunk tag.
        /// </summary>
        private static readonly byte[] TRUNCATED_WEBP_BYTES = [0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42];

        /// <summary>
        /// System under test validating binary signatures against declared raster MIME types.
        /// </summary>
        private readonly MagicByteImageSignatureValidator _validator = new();

        /// <summary>
        /// Verifies that valid PNG, JPEG, and WebP magic headers return <see langword="true"/> for their canonical MIME types.
        /// </summary>
        [Fact]
        public void IsValidSignature_WhenHeadersMatchDeclaredRasterTypes_ReturnsTrue()
        {
                // Act & Assert
                Assert.True(this._validator.IsValidSignature(headerBytes: VALID_PNG_BYTES, declaredContentType: DomainConstants.CONTENT_TYPE_PNG));
                Assert.True(this._validator.IsValidSignature(headerBytes: VALID_JPEG_BYTES, declaredContentType: DomainConstants.CONTENT_TYPE_JPEG));
                Assert.True(this._validator.IsValidSignature(headerBytes: VALID_WEBP_BYTES, declaredContentType: DomainConstants.CONTENT_TYPE_WEBP));
                // Content-type matching trims surrounding whitespace and ignores ASCII casing.
                Assert.True(this._validator.IsValidSignature(headerBytes: VALID_PNG_BYTES, declaredContentType: "  IMAGE/PNG  "));
        }

        /// <summary>
        /// Verifies that mismatched magic bytes and MIME declarations return <see langword="false"/>.
        /// </summary>
        [Fact]
        public void IsValidSignature_WhenHeaderDoesNotMatchDeclaredMimeType_ReturnsFalse()
        {
                // Act & Assert
                // Cross-format spoofing (e.g., PNG bytes claiming to be JPEG) must be rejected.
                Assert.False(this._validator.IsValidSignature(headerBytes: VALID_PNG_BYTES, declaredContentType: DomainConstants.CONTENT_TYPE_JPEG));
                Assert.False(this._validator.IsValidSignature(headerBytes: VALID_JPEG_BYTES, declaredContentType: DomainConstants.CONTENT_TYPE_WEBP));
                Assert.False(this._validator.IsValidSignature(headerBytes: VALID_WEBP_BYTES, declaredContentType: DomainConstants.CONTENT_TYPE_PNG));
        }

        /// <summary>
        /// Verifies that active-content SVG/HTML payloads and disallowed or parameterized MIME strings are rejected.
        /// </summary>
        /// <param name="payloadText">The disguised text payload.</param>
        /// <param name="declaredContentType">The declared MIME content type.</param>
        [Theory]
        [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>", "image/svg+xml")]
        [InlineData("<svg><script>alert(1)</script></svg>", DomainConstants.CONTENT_TYPE_PNG)]
        [InlineData("<html><body>bad</body></html>", "text/html")]
        [InlineData("<html><body>bad</body></html>", DomainConstants.CONTENT_TYPE_JPEG)]
        public void IsValidSignature_WhenSvgOrHtmlPayloads_ReturnsFalse(string payloadText, string declaredContentType)
        {
                // Arrange
                // Encode active markup payloads as UTF-8 bytes to simulate XSS upload attempts.
                byte[] payloadBytes = Encoding.UTF8.GetBytes(payloadText);

                // Act
                bool isValid = this._validator.IsValidSignature(headerBytes: payloadBytes, declaredContentType: declaredContentType);

                // Assert
                Assert.False(isValid);
        }

        /// <summary>
        /// Verifies that unknown MIME types, empty inputs, and truncated headers return <see langword="false"/>.
        /// </summary>
        [Fact]
        public void IsValidSignature_WhenEmptyTruncatedOrUnsupportedMime_ReturnsFalse()
        {
                // Act & Assert
                Assert.False(this._validator.IsValidSignature(headerBytes: ReadOnlySpan<byte>.Empty, declaredContentType: DomainConstants.CONTENT_TYPE_PNG));
                Assert.False(this._validator.IsValidSignature(headerBytes: TRUNCATED_PNG_BYTES, declaredContentType: DomainConstants.CONTENT_TYPE_PNG));
                Assert.False(this._validator.IsValidSignature(headerBytes: TRUNCATED_JPEG_BYTES, declaredContentType: DomainConstants.CONTENT_TYPE_JPEG));
                Assert.False(this._validator.IsValidSignature(headerBytes: TRUNCATED_WEBP_BYTES, declaredContentType: DomainConstants.CONTENT_TYPE_WEBP));
                // Reject parameterized MIME types, non-canonical aliases (image/jpg), and blank types.
                Assert.False(this._validator.IsValidSignature(headerBytes: VALID_PNG_BYTES, declaredContentType: "image/png; charset=utf-8"));
                Assert.False(this._validator.IsValidSignature(headerBytes: VALID_JPEG_BYTES, declaredContentType: "image/jpg"));
                Assert.False(this._validator.IsValidSignature(headerBytes: VALID_PNG_BYTES, declaredContentType: string.Empty));
        }
}

