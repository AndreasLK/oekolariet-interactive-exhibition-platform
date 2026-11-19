using ContentService.Domain.Common;
using ContentService.Domain.Exceptions;

namespace ContentService.Domain.Entities;

/// <summary>
/// Represents the binary payload associated with an <see cref="ImageEntity"/>.
/// </summary>
public sealed class ImageBinaryEntity
{
        private ImageBinaryEntity()
        {
        }

        /// <summary>
        /// Gets the identifier of the owning <see cref="ImageEntity"/>.
        /// </summary>
        public Guid ImageId { get; private set; }

        /// <summary>
        /// Gets the raw binary bytes of the image.
        /// </summary>
        public byte[] Data { get; private set; } = [];

        /// <summary>
        /// Creates a new <see cref="ImageBinaryEntity"/> for the specified image identifier and binary payload.
        /// </summary>
        /// <param name="imageId">The identifier of the owning <see cref="ImageEntity"/>.</param>
        /// <param name="data">The raw binary bytes of the image.</param>
        /// <returns>A newly initialized <see cref="ImageBinaryEntity"/>.</returns>
        public static ImageBinaryEntity Create(Guid imageId, byte[] data)
        {
                if (imageId == Guid.Empty)
                {
                        throw new DomainValidationException(message: "Image ID cannot be empty.");
                }

                if (data is null || data.Length < DomainConstants.MIN_IMAGE_SIZE_BYTES || data.Length > DomainConstants.MAX_IMAGE_SIZE_BYTES)
                {
                        throw new DomainValidationException(
                                message: $"Image binary size must be between {DomainConstants.MIN_IMAGE_SIZE_BYTES} and {DomainConstants.MAX_IMAGE_SIZE_BYTES} bytes.");
                }

                return new ImageBinaryEntity
                {
                        ImageId = imageId,
                        Data = [.. data]
                };
        }
}
