using ContentService.Domain.Common;
using ContentService.Domain.Entities;
using ContentService.Domain.Exceptions;

namespace ContentService.Tests;

/// <summary>
/// Unit tests for <see cref="ImageEntity"/> invariant validation, normalization, and soft-delete lifecycle.
/// </summary>
public sealed class ImageEntityTests
{
        private const string VALID_NODE_ID = "node-1";
        private const string VALID_FILE_NAME = "exhibit.png";
        private const string VALID_ACTOR_ID = "curator-1";
        private static readonly string VALID_SHA256 = new('a', DomainConstants.SHA256_HEX_LENGTH);

        /// <summary>
        /// Verifies that <see cref="ImageEntity.Create"/> normalizes content type, SHA-256 hash, file name, and blank alt text.
        /// </summary>
        [Fact]
        public void Create_WhenValidInputs_NormalizesFieldsAndClearsBlankAltText()
        {
                // Arrange
                string uppercaseHash = new('A', DomainConstants.SHA256_HEX_LENGTH);

                // Act
                ImageEntity image = ImageEntity.Create(
                        storageNodeId: VALID_NODE_ID,
                        fileName: $"  {VALID_FILE_NAME}  ",
                        contentType: "  IMAGE/PNG  ",
                        fileSizeBytes: DomainConstants.MIN_IMAGE_SIZE_BYTES,
                        sha256Hash: $"  {uppercaseHash}  ",
                        actorId: VALID_ACTOR_ID,
                        altText: "   ");

                // Assert
                Assert.Equal(VALID_FILE_NAME, image.FileName);
                Assert.Equal(DomainConstants.CONTENT_TYPE_PNG, image.ContentType);
                Assert.Equal(VALID_SHA256, image.Sha256Hash);
                Assert.Null(image.AltText);
                Assert.False(image.IsDeleted);
        }

        /// <summary>
        /// Verifies that file names containing path-traversal segments or control characters are rejected.
        /// </summary>
        /// <param name="unsafeFileName">An unsafe file name.</param>
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("../secret.png")]
        [InlineData(@"sub\photo.png")]
        [InlineData("sub/photo.png")]
        [InlineData("photo..png")]
        [InlineData("photo\n.png")]
        public void Create_WhenFileNameUnsafeOrBlank_ThrowsDomainValidationException(string unsafeFileName)
        {
                // Act & Assert
                Assert.Throws<DomainValidationException>(() => ImageEntity.Create(
                        storageNodeId: VALID_NODE_ID,
                        fileName: unsafeFileName,
                        contentType: DomainConstants.CONTENT_TYPE_PNG,
                        fileSizeBytes: 1024,
                        sha256Hash: VALID_SHA256,
                        actorId: VALID_ACTOR_ID));
        }

        /// <summary>
        /// Verifies that disallowed MIME types such as SVG (XSS risk) or GIF are rejected.
        /// </summary>
        /// <param name="disallowedContentType">A disallowed MIME type.</param>
        [Theory]
        [InlineData("image/svg+xml")]
        [InlineData("image/gif")]
        [InlineData("")]
        public void Create_WhenContentTypeDisallowed_ThrowsDomainValidationException(string disallowedContentType)
        {
                // Act & Assert
                Assert.Throws<DomainValidationException>(() => ImageEntity.Create(
                        storageNodeId: VALID_NODE_ID,
                        fileName: VALID_FILE_NAME,
                        contentType: disallowedContentType,
                        fileSizeBytes: 1024,
                        sha256Hash: VALID_SHA256,
                        actorId: VALID_ACTOR_ID));
        }

        /// <summary>
        /// Verifies that image byte sizes outside <see cref="DomainConstants.MIN_IMAGE_SIZE_BYTES"/> and <see cref="DomainConstants.MAX_IMAGE_SIZE_BYTES"/> are rejected.
        /// </summary>
        /// <param name="invalidSize">An out-of-range byte size.</param>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(DomainConstants.MAX_IMAGE_SIZE_BYTES + 1)]
        public void Create_WhenFileSizeOutOfRange_ThrowsDomainValidationException(long invalidSize)
        {
                // Act & Assert
                Assert.Throws<DomainValidationException>(() => ImageEntity.Create(
                        storageNodeId: VALID_NODE_ID,
                        fileName: VALID_FILE_NAME,
                        contentType: DomainConstants.CONTENT_TYPE_PNG,
                        fileSizeBytes: invalidSize,
                        sha256Hash: VALID_SHA256,
                        actorId: VALID_ACTOR_ID));
        }

        /// <summary>
        /// Verifies that invalid SHA-256 digests (wrong length or non-hex characters) are rejected.
        /// </summary>
        /// <param name="invalidHash">An invalid SHA-256 string.</param>
        [Fact]
        public void Create_WhenSha256HashInvalid_ThrowsDomainValidationException()
        {
                // Arrange
                string nonHexHash = $"{new string('a', DomainConstants.SHA256_HEX_LENGTH - 1)}g";

                // Act & Assert
                Assert.Throws<DomainValidationException>(() => ImageEntity.Create(
                        storageNodeId: VALID_NODE_ID,
                        fileName: VALID_FILE_NAME,
                        contentType: DomainConstants.CONTENT_TYPE_PNG,
                        fileSizeBytes: 1024,
                        sha256Hash: nonHexHash,
                        actorId: VALID_ACTOR_ID));
        }

        /// <summary>
        /// Verifies that <see cref="ImageEntity.MarkDeleted"/> sets <see cref="ImageEntity.IsDeleted"/> and blocks subsequent edits or repeat deletions.
        /// </summary>
        [Fact]
        public void MarkDeleted_WhenDeleted_BlocksUpdateAltTextAndRepeatDelete()
        {
                // Arrange
                ImageEntity image = CreateValidImage();

                // Act
                image.MarkDeleted(VALID_ACTOR_ID);

                // Assert
                Assert.True(image.IsDeleted);
                Assert.Throws<DomainValidationException>(() => image.UpdateAltText("New alt", VALID_ACTOR_ID));
                Assert.Throws<DomainValidationException>(() => image.MarkDeleted(VALID_ACTOR_ID));
        }

        /// <summary>
        /// Verifies that <see cref="ImageEntity.UpdateAltText"/> leaves state unchanged when validation fails.
        /// </summary>
        [Fact]
        public void UpdateAltText_WhenExceedsMaxLength_LeavesExistingAltTextUnchanged()
        {
                // Arrange
                ImageEntity image = CreateValidImage();
                string oversizedAltText = new('x', DomainConstants.MAX_ALT_TEXT_LENGTH + 1);

                // Act & Assert
                Assert.Throws<DomainValidationException>(() => image.UpdateAltText(oversizedAltText, VALID_ACTOR_ID));
                Assert.Equal("Initial alt", image.AltText);
        }

        private static ImageEntity CreateValidImage()
        {
                return ImageEntity.Create(
                        storageNodeId: VALID_NODE_ID,
                        fileName: VALID_FILE_NAME,
                        contentType: DomainConstants.CONTENT_TYPE_PNG,
                        fileSizeBytes: 1024,
                        sha256Hash: VALID_SHA256,
                        actorId: VALID_ACTOR_ID,
                        altText: "Initial alt");
        }
}

