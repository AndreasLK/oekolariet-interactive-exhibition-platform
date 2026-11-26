using ContentService.Domain.Common;
using ContentService.Domain.Exceptions;

namespace ContentService.Domain.Entities;

/// <summary>
/// Stores the raw binary payload of an uploaded image separately from <see cref="ImageEntity"/> metadata.
/// </summary>
// Split from ImageEntity (1:1) so metadata queries never pull megabytes of raw image bytes into memory, while keeping on-prem museum nodes self-contained in SQL Server.
// Omits BaseEntity because binary bytes are immutable and share ImageEntity's lifecycle via the ImageId primary/foreign key.
public sealed class ImageBinaryEntity
{
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageBinaryEntity"/> class for EF Core.
        /// </summary>
        private ImageBinaryEntity()
        {
        }

        /// <summary>
        /// Gets the identifier of the owning <see cref="ImageEntity"/>.
        /// </summary>
        public Guid ImageId { get; private set; }

        /// <summary>
        /// Gets the raw image file bytes.
        /// </summary>
        // Stored as byte[] for direct EF Core binary column mapping; max size is capped at 10 MiB by DomainConstants.MAX_IMAGE_SIZE_BYTES.
        public byte[] Data { get; private set; } = [];

        /// <summary>
        /// Gets the pre-generated lightweight preview image bytes, or <see langword="null"/> if not yet generated.
        /// </summary>
        public byte[]? PreviewData { get; private set; }

        /// <summary>
        /// Gets the MIME content type of <see cref="PreviewData"/> (for example, <c>image/webp</c>).
        /// </summary>
        public string? PreviewContentType { get; private set; }

        /// <summary>
        /// Creates a validated <see cref="ImageBinaryEntity"/> for an existing image identity.
        /// </summary>
        /// <param name="imageId">The non-empty identifier of the owning <see cref="ImageEntity"/>.</param>
        /// <param name="data">The raw image bytes within configured minimum and maximum size limits.</param>
        /// <returns>A newly constructed <see cref="ImageBinaryEntity"/> containing a defensive copy of <paramref name="data"/>.</returns>
        /// <exception cref="DomainValidationException">Thrown when <paramref name="imageId"/> is empty, or <paramref name="data"/> is null or outside allowed size limits.</exception>
        public static ImageBinaryEntity Create(Guid imageId, byte[] data)
        {
                if (imageId == Guid.Empty)
                {
                        throw new DomainValidationException("Image ID cannot be empty.");
                }

                // Throws DomainValidationException for null data as well so API error middleware maps all upload validation failures uniformly.
                if (data is null || data.Length < DomainConstants.MIN_IMAGE_SIZE_BYTES || data.Length > DomainConstants.MAX_IMAGE_SIZE_BYTES)
                {
                        throw new DomainValidationException(
                                $"Image binary size must be between {DomainConstants.MIN_IMAGE_SIZE_BYTES} and {DomainConstants.MAX_IMAGE_SIZE_BYTES} bytes.");
                }

                return new ImageBinaryEntity
                {
                        ImageId = imageId,
                        // Copy the byte array so later mutations to the caller's buffer cannot alter the entity's stored bytes.
                        Data = [.. data]
                };
        }

        /// <summary>
        /// Attaches or replaces the pre-generated lightweight preview payload for fast gallery rendering.
        /// </summary>
        /// <param name="previewData">The encoded preview image bytes.</param>
        /// <param name="previewContentType">The MIME content type of the preview payload.</param>
        /// <exception cref="DomainValidationException">Thrown when <paramref name="previewData"/> is empty or <paramref name="previewContentType"/> is blank.</exception>
        public void SetPreview(byte[] previewData, string previewContentType)
        {
                if (previewData is null || previewData.Length == 0)
                {
                        throw new DomainValidationException("Preview binary data cannot be empty.");
                }

                if (string.IsNullOrWhiteSpace(previewContentType))
                {
                        throw new DomainValidationException("Preview content type cannot be empty.");
                }

                this.PreviewData = [.. previewData];
                this.PreviewContentType = previewContentType.Trim();
        }
}
