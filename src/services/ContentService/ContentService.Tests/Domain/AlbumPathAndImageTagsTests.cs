using ContentService.Domain.Entities;
using ContentService.Domain.Exceptions;
using ContentService.Domain.ValueObjects;

namespace ContentService.Tests.Domain;

/// <summary>
/// Unit tests verifying recursive <see cref="AlbumPath"/> validation, <see cref="ImageTags"/> normalization, and <see cref="ImageEntity.UpdateMetadata"/>.
/// </summary>
public sealed class AlbumPathAndImageTagsTests
{
        private const string ACTOR_ID = "domain-tester";

        /// <summary>
        /// Verifies <see cref="AlbumPath.NormalizeOptional"/> normalizes slashes and Danish characters while rejecting traversal segments.
        /// </summary>
        [Fact]
        public void NormalizeOptional_ValidAndInvalidPaths_NormalizesOrThrowsValidationException()
        {
                // Arrange & Act
                string? normalized = AlbumPath.NormalizeOptional(rawAlbumPath: @" \Madaffald\Frugt & Grønt/ ");
                string? blank = AlbumPath.NormalizeOptional(rawAlbumPath: "  /  ");

                // Assert
                Assert.Equal(expected: "Madaffald/Frugt & Grønt", actual: normalized);
                Assert.Null(@object: blank);
                Assert.Throws<DomainValidationException>(testCode: () => AlbumPath.NormalizeOptional(rawAlbumPath: "Madaffald/../Secrets"));
                Assert.Throws<DomainValidationException>(testCode: () => AlbumPath.NormalizeOptional(rawAlbumPath: "Madaffald//Frugt"));
        }

        /// <summary>
        /// Verifies <see cref="AlbumPath.ExtractDirectoryFromRelativeFilePath"/> and <see cref="AlbumPath.Combine"/> preserve nested upload folders.
        /// </summary>
        [Fact]
        public void ExtractAndCombine_RelativeFolderPaths_PreserveSubdirectories()
        {
                // Act
                string? extracted = AlbumPath.ExtractDirectoryFromRelativeFilePath(relativeFilePath: "Frugt/Citrus/appelsin.png");
                string? leafOnly = AlbumPath.ExtractDirectoryFromRelativeFilePath(relativeFilePath: "banana.png");
                string? combined = AlbumPath.Combine(baseAlbumPath: "Madaffald", subAlbumPath: extracted);

                // Assert
                Assert.Equal(expected: "Frugt/Citrus", actual: extracted);
                Assert.Null(@object: leafOnly);
                Assert.Equal(expected: "Madaffald/Frugt/Citrus", actual: combined);
        }

        /// <summary>
        /// Verifies <see cref="ImageTags.NormalizeOptional"/> deduplicates case-insensitively and updates via <see cref="ImageEntity.UpdateMetadata"/>.
        /// </summary>
        [Fact]
        public void UpdateMetadata_ValidInputs_NormalizesTagsAlbumAndRotatesStamp()
        {
                // Arrange
                ImageEntity image = ImageEntity.Create(
                        storageNodeId: "node-1",
                        fileName: "banana.png",
                        contentType: "image/png",
                        fileSizeBytes: 512,
                        sha256Hash: new string(c: 'a', count: 64),
                        actorId: ACTOR_ID);
                Guid initialStamp = image.ConcurrencyStamp;

                // Act
                image.UpdateMetadata(
                        altText: "Yellow banana for sorting quiz",
                        tags: " Banana , fruit, banana , Madaffald ",
                        albumPath: "Madaffald/Frugt",
                        actorId: ACTOR_ID);

                // Assert
                Assert.Equal(expected: "Yellow banana for sorting quiz", actual: image.AltText);
                Assert.Equal(expected: "Madaffald/Frugt", actual: image.AlbumPath);
                Assert.Equal(expected: "banana,fruit,madaffald", actual: image.Tags);
                Assert.Equal(expected: ["banana", "fruit", "madaffald"], actual: image.TagList);
                Assert.NotEqual(expected: initialStamp, actual: image.ConcurrencyStamp);
        }
}

