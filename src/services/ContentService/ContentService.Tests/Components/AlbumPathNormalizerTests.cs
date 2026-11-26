using ContentService.Components.Internal;

namespace ContentService.Tests.Components;

/// <summary>
/// Unit tests verifying recursive album path normalization and hierarchy helpers in <see cref="AlbumPathNormalizer"/>.
/// </summary>
public sealed class AlbumPathNormalizerTests
{
        /// <summary>
        /// Verifies relative directory extraction and album combination helpers preserve recursive subfolder structure.
        /// </summary>
        [Fact]
        public void AlbumHelpers_ExtractAndCombine_NormalizeRecursiveFolders()
        {
                // Act
                string? extracted = AlbumPathNormalizer.ExtractRelativeDirectory(relativeFilePath: @"Madaffald\Frugt\banana.png");
                string? combined = AlbumPathNormalizer.Combine(baseAlbumPath: "Quiz", subAlbumPath: extracted);
                string leaf = AlbumPathNormalizer.FormatLeafName(fullAlbumPath: "Quiz/Madaffald/Frugt");

                // Assert
                Assert.Equal(expected: "Madaffald/Frugt", actual: extracted);
                Assert.Equal(expected: "Quiz/Madaffald/Frugt", actual: combined);
                Assert.Equal(expected: "Frugt", actual: leaf);
        }

        /// <summary>
        /// Verifies blank paths return <see langword="null"/> and relative traversal segments are stripped safely for UI rendering.
        /// </summary>
        [Theory]
        [InlineData(null, null)]
        [InlineData("   ", null)]
        [InlineData(@"./Madaffald\..\Frugt/", "Madaffald/Frugt")]
        public void Normalize_WhenBlankOrContainingTraversal_ReturnsSafeNormalizedPath(
                string? rawAlbumPath,
                string? expected)
        {
                // Act
                string? actual = AlbumPathNormalizer.Normalize(rawAlbumPath: rawAlbumPath);

                // Assert
                Assert.Equal(expected: expected, actual: actual);
        }
}

