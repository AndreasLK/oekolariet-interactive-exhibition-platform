using ContentService.Domain.Common;
using ContentService.Domain.Exceptions;

namespace ContentService.Domain.Entities;

// Separated from ImageEntity (SRP) so metadata queries never load multi-megabyte byte[] payloads into memory; EF Core maps this to a dedicated table/columns.
public sealed class ImageBinaryEntity
{
        private ImageBinaryEntity()
        {
        }

        public Guid ImageId { get; private set; }

        public byte[] Data { get; private set; } = [];

        // Factory method enforces size and identity invariants before construction so an invalid binary entity can never exist in memory.
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
                        // Defensive copy via collection expression prevents callers from mutating the entity's internal buffer after creation.
                        Data = [.. data]
                };
        }
}
