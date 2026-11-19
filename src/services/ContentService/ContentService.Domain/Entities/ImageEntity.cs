using ContentService.Domain.Common;
using ContentService.Domain.Exceptions;

namespace ContentService.Domain.Entities;

/// <summary>
/// Represents an uploaded image asset and its metadata in the Content Service.
/// </summary>
public sealed class ImageEntity : BaseEntity
{
        private ImageEntity()
        {
        }

        /// <summary>
        /// Gets the original file name of the image.
        /// </summary>
        public string FileName { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the MIME content type of the image.
        /// </summary>
        public string ContentType { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the size of the image binary in bytes.
        /// </summary>
        public long FileSizeBytes { get; private set; }

        /// <summary>
        /// Gets the lowercase hexadecimal SHA-256 hash of the image binary.
        /// </summary>
        public string Sha256Hash { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the optional accessibility alternative text for the image.
        /// </summary>
        public string? AltText { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the image has been soft-deleted.
        /// </summary>
        public bool IsDeleted { get; private set; }

        /// <summary>
        /// Creates a new <see cref="ImageEntity"/> with validated metadata.
        /// </summary>
        /// <param name="fileName">The original file name of the image.</param>
        /// <param name="contentType">The MIME content type of the image.</param>
        /// <param name="fileSizeBytes">The size of the image in bytes.</param>
        /// <param name="sha256Hash">The SHA-256 hash of the image binary.</param>
        /// <param name="actorId">The identifier of the actor uploading the image.</param>
        /// <param name="altText">Optional accessibility alternative text.</param>
        /// <returns>A newly initialized <see cref="ImageEntity"/>.</returns>
        public static ImageEntity Create(
                string fileName,
                string contentType,
                long fileSizeBytes,
                string sha256Hash,
                string actorId,
                string? altText = null)
        {
                string validatedFileName = ValidateFileName(fileName: fileName);
                string validatedContentType = ValidateContentType(contentType: contentType);
                ValidateFileSize(fileSizeBytes: fileSizeBytes);
                string validatedHash = ValidateSha256Hash(sha256Hash: sha256Hash);
                string? validatedAltText = ValidateAltText(altText: altText);

                ImageEntity entity = new()
                {
                        FileName = validatedFileName,
                        ContentType = validatedContentType,
                        FileSizeBytes = fileSizeBytes,
                        Sha256Hash = validatedHash,
                        AltText = validatedAltText,
                        IsDeleted = false
                };

                entity.InitializeAudit(actorId: actorId);
                return entity;
        }

        /// <summary>
        /// Updates the accessibility alternative text for the image.
        /// </summary>
        /// <param name="altText">The new alternative text, or <see langword="null"/> to clear it.</param>
        /// <param name="actorId">The identifier of the actor performing the update.</param>
        public void UpdateAltText(string? altText, string actorId)
        {
                if (this.IsDeleted)
                {
                        throw new DomainValidationException(message: "Cannot update alternative text of a deleted image.");
                }

                this.AltText = ValidateAltText(altText: altText);
                this.MarkModified(actorId: actorId);
        }

        /// <summary>
        /// Marks the image as soft-deleted.
        /// </summary>
        /// <param name="actorId">The identifier of the actor deleting the image.</param>
        public void MarkDeleted(string actorId)
        {
                if (this.IsDeleted)
                {
                        throw new DomainValidationException(message: "Image is already deleted.");
                }

                this.IsDeleted = true;
                this.MarkModified(actorId: actorId);
        }

        private static string ValidateFileName(string fileName)
        {
                if (string.IsNullOrWhiteSpace(value: fileName))
                {
                        throw new DomainValidationException(message: "File name cannot be null or whitespace.");
                }

                string trimmed = fileName.Trim();
                if (trimmed.Length > DomainConstants.MAX_FILE_NAME_LENGTH)
                {
                        throw new DomainValidationException(
                                message: $"File name cannot exceed {DomainConstants.MAX_FILE_NAME_LENGTH} characters.");
                }

                return trimmed;
        }

        private static string ValidateContentType(string contentType)
        {
                if (string.IsNullOrWhiteSpace(value: contentType))
                {
                        throw new DomainValidationException(message: "Content type cannot be null or whitespace.");
                }

                string trimmed = contentType.Trim();
                if (!DomainConstants.ALLOWED_IMAGE_CONTENT_TYPES.Contains(item: trimmed))
                {
                        throw new DomainValidationException(
                                message: $"Content type '{trimmed}' is not an allowed image content type.");
                }

                return trimmed.ToLowerInvariant();
        }

        private static void ValidateFileSize(long fileSizeBytes)
        {
                if (fileSizeBytes < DomainConstants.MIN_IMAGE_SIZE_BYTES || fileSizeBytes > DomainConstants.MAX_IMAGE_SIZE_BYTES)
                {
                        throw new DomainValidationException(
                                message: $"File size must be between {DomainConstants.MIN_IMAGE_SIZE_BYTES} and {DomainConstants.MAX_IMAGE_SIZE_BYTES} bytes.");
                }
        }

        private static string ValidateSha256Hash(string sha256Hash)
        {
                if (string.IsNullOrWhiteSpace(value: sha256Hash))
                {
                        throw new DomainValidationException(message: "SHA-256 hash cannot be null or whitespace.");
                }

                string trimmed = sha256Hash.Trim();
                if (trimmed.Length != DomainConstants.SHA256_HEX_LENGTH)
                {
                        throw new DomainValidationException(
                                message: $"SHA-256 hash must be exactly {DomainConstants.SHA256_HEX_LENGTH} hexadecimal characters.");
                }

                return trimmed.ToLowerInvariant();
        }

        private static string? ValidateAltText(string? altText)
        {
                if (string.IsNullOrWhiteSpace(value: altText))
                {
                        return null;
                }

                string trimmed = altText.Trim();
                if (trimmed.Length > DomainConstants.MAX_ALT_TEXT_LENGTH)
                {
                        throw new DomainValidationException(
                                message: $"Alternative text cannot exceed {DomainConstants.MAX_ALT_TEXT_LENGTH} characters.");
                }

                return trimmed;
        }
}
