using ContentService.Components.Internal;

namespace ContentService.Tests.Components;

/// <summary>
/// Unit tests verifying browser MIME type normalization in <see cref="ImageContentTypeNormalizer"/>.
/// </summary>
public sealed class ImageContentTypeNormalizerTests
{
        /// <summary>
        /// Verifies browser-reported MIME types normalize to ContentService's canonical raster allowlist.
        /// </summary>
        /// <param name="rawContentType">The browser-supplied MIME type.</param>
        /// <param name="fileName">The uploaded file name.</param>
        /// <param name="expectedMime">The expected normalized MIME type.</param>
        [Theory]
        [InlineData("image/jpg", "photo.jpg", "image/jpeg")]
        [InlineData("IMAGE/PNG ", "diagram.PNG", "image/png")]
        [InlineData("", "banner.webp", "image/webp")]
        [InlineData("application/octet-stream", "chart.jpeg", "image/jpeg")]
        [InlineData("", "notes.txt", "application/octet-stream")]
        public void Normalize_NormalizesBrowserVariantsAndFallbacks(
                string rawContentType,
                string fileName,
                string expectedMime)
        {
                // Act
                string actual = ImageContentTypeNormalizer.Normalize(
                        rawContentType: rawContentType,
                        fileName: fileName);

                // Assert
                Assert.Equal(expected: expectedMime, actual: actual);
        }
}

