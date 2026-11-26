using ContentService.Components.Internal;
using ContentService.Sdk.Models;

namespace ContentService.Tests.Components;

/// <summary>
/// Unit tests verifying byte-size and gallery tile subtitle formatting in <see cref="ContentImageDisplayFormatter"/>.
/// </summary>
public sealed class ContentImageDisplayFormatterTests
{
        private const long SMALL_BYTE_SIZE = 512L;
        private const long EXACT_KIBIBYTE = 1024L;
        private const long TWO_MEBIBYTES = 2L * 1024L * 1024L;

        /// <summary>
        /// Verifies byte sizes format into B, KB, and MB units cleanly across binary thresholds.
        /// </summary>
        /// <param name="sizeBytes">The raw payload byte length.</param>
        /// <param name="expectedFormatted">The expected human-readable label.</param>
        [Theory]
        [InlineData(SMALL_BYTE_SIZE, "512 B")]
        [InlineData(EXACT_KIBIBYTE, "1 KB")]
        [InlineData(TWO_MEBIBYTES, "2 MB")]
        public void FormatByteSize_FormatsAcrossThresholds(
                long sizeBytes,
                string expectedFormatted)
        {
                // Act
                string actual = ContentImageDisplayFormatter.FormatByteSize(sizeBytes: sizeBytes);

                // Assert
                Assert.Equal(expected: expectedFormatted, actual: actual);
        }

        /// <summary>
        /// Verifies <see cref="ContentImageDisplayFormatter.FormatTileSubtitle"/> throws on null input and formats valid metadata.
        /// </summary>
        [Fact]
        public void FormatTileSubtitle_WhenValidOrNull_FormatsOrThrowsArgumentNullException()
        {
                // Arrange
                DateTimeOffset createdAt = new(
                        year: 2026,
                        month: 11,
                        day: 22,
                        hour: 12,
                        minute: 0,
                        second: 0,
                        offset: TimeSpan.Zero);
                ContentImageMetadata metadata = new()
                {
                        Id = Guid.NewGuid(),
                        PictureReference = $"picture.node-1.{Guid.NewGuid():D}",
                        ExternalId = $"node-1_{Guid.NewGuid():D}",
                        FileName = "coral.png",
                        ContentType = "image/png",
                        SizeBytes = EXACT_KIBIBYTE,
                        Sha256Hash = new string(c: 'c', count: 64),
                        CreatedAt = createdAt,
                        ConcurrencyStamp = Guid.NewGuid()
                };

                // Act
                string subtitle = ContentImageDisplayFormatter.FormatTileSubtitle(image: metadata);

                // Assert
                Assert.StartsWith(expectedStartString: "1 KB • ", actualString: subtitle);
                Assert.Throws<ArgumentNullException>(testCode: () => ContentImageDisplayFormatter.FormatTileSubtitle(image: null!));
        }
}

