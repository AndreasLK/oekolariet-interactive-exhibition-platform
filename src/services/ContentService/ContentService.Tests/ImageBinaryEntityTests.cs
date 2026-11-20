using ContentService.Domain.Common;
using ContentService.Domain.Entities;
using ContentService.Domain.Exceptions;

namespace ContentService.Tests;

/// <summary>
/// Unit tests for <see cref="ImageBinaryEntity"/> size guards and defensive byte-array copying.
/// </summary>
public sealed class ImageBinaryEntityTests
{
        private static readonly byte[] SAMPLE_BYTES = [1, 2, 3, 4];

        /// <summary>
        /// Verifies that mutating the caller's byte array after <see cref="ImageBinaryEntity.Create"/> does not alter stored bytes.
        /// </summary>
        [Fact]
        public void Create_WhenCallerMutatesInputArray_PreservesStoredBytes()
        {
                // Arrange
                Guid imageId = Guid.NewGuid();
                byte[] buffer = [10, 20, 30];

                // Act
                ImageBinaryEntity binary = ImageBinaryEntity.Create(imageId, buffer);
                buffer[0] = 99;

                // Assert
                Assert.Equal(imageId, binary.ImageId);
                Assert.Equal((byte)10, binary.Data[0]);
        }

        /// <summary>
        /// Verifies that <see cref="Guid.Empty"/> is rejected with <see cref="DomainValidationException"/>.
        /// </summary>
        [Fact]
        public void Create_WhenImageIdIsEmpty_ThrowsDomainValidationException()
        {
                // Act & Assert
                Assert.Throws<DomainValidationException>(() => ImageBinaryEntity.Create(Guid.Empty, SAMPLE_BYTES));
        }

        /// <summary>
        /// Verifies that null, empty, or oversized byte payloads throw <see cref="DomainValidationException"/>.
        /// </summary>
        [Fact]
        public void Create_WhenDataNullOrEmpty_ThrowsDomainValidationException()
        {
                // Arrange
                Guid imageId = Guid.NewGuid();

                // Act & Assert
                Assert.Throws<DomainValidationException>(() => ImageBinaryEntity.Create(imageId, null!));
                Assert.Throws<DomainValidationException>(() => ImageBinaryEntity.Create(imageId, []));
        }

        /// <summary>
        /// Verifies that payloads exceeding <see cref="DomainConstants.MAX_IMAGE_SIZE_BYTES"/> are rejected.
        /// </summary>
        [Fact]
        public void Create_WhenDataExceedsMaxSize_ThrowsDomainValidationException()
        {
                // Arrange
                Guid imageId = Guid.NewGuid();
                byte[] oversized = new byte[DomainConstants.MAX_IMAGE_SIZE_BYTES + 1];

                // Act & Assert
                Assert.Throws<DomainValidationException>(() => ImageBinaryEntity.Create(imageId, oversized));
        }
}

