using ContentService.Domain.Common;
using ContentService.Domain.Exceptions;
using ContentService.Domain.ValueObjects;

namespace ContentService.Domain.Entities;

public sealed class ImageEntity : BaseEntity
{
        private ImageEntity()
        {
        }

        public string NodeId { get; private set; } = string.Empty;

        public string FileName { get; private set; } = string.Empty;

        public string ContentType { get; private set; } = string.Empty;

        public long FileSizeBytes { get; private set; }

        public string Sha256Hash { get; private set; } = string.Empty;

        public string? AltText { get; private set; }

        public bool IsDeleted { get; private set; }

        public string ExternalId => $"{this.NodeId}{DomainConstants.ASSET_ID_SEPARATOR}{this.Id.ToString(format: DomainConstants.GUID_FORMAT_HYPHENATED)}";

        public PictureHandle Handle => new(nodeId: this.NodeId, imageId: this.Id);

        public static ImageEntity Create(
                string nodeId,
                string fileName,
                string contentType,
                long fileSizeBytes,
                string sha256Hash,
                string actorId,
                string? altText = null)
        {
                string validatedNodeId = PictureHandle.ValidateNodeId(nodeId: nodeId);
                string validatedFileName = ValidateFileName(fileName: fileName);
                string validatedContentType = ValidateContentType(contentType: contentType);
                ValidateFileSize(fileSizeBytes: fileSizeBytes);
                string validatedHash = ValidateSha256Hash(sha256Hash: sha256Hash);
                string? validatedAltText = ValidateAltText(altText: altText);

                ImageEntity entity = new()
                {
                        NodeId = validatedNodeId,
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

        public void UpdateAltText(string? altText, string actorId)
        {
                if (this.IsDeleted)
                {
                        throw new DomainValidationException(message: "Cannot update alternative text of a deleted image.");
                }

                this.AltText = ValidateAltText(altText: altText);
                this.MarkModified(actorId: actorId);
        }

        // Soft-delete is used instead of physical deletion because Drafts live in UI Editor's separate database, so hard-deleting could break active drafts across service boundaries.
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

                // OWASP path-traversal protection for downstream consumers and offline kiosk caches that derive local file paths from image metadata.
                if (ContainsForbiddenPathCharacters(fileName: trimmed))
                {
                        throw new DomainValidationException(
                                message: "File name cannot contain path separators, parent-directory traversal segments, or control characters.");
                }

                return trimmed;
        }

        private static bool ContainsForbiddenPathCharacters(string fileName)
        {
                foreach (string forbiddenSegment in DomainConstants.FORBIDDEN_FILE_NAME_SEGMENTS)
                {
                        if (fileName.Contains(value: forbiddenSegment, comparisonType: StringComparison.Ordinal))
                        {
                                return true;
                        }
                }

                foreach (char character in fileName)
                {
                        if (char.IsControl(c: character))
                        {
                                return true;
                        }
                }

                return false;
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
                // Sha256Hash is emitted verbatim in HTTP ETag headers and used for offline kiosk cache deduplication, so non-hex characters risk header injection or broken cache matching.
                if (trimmed.Length != DomainConstants.SHA256_HEX_LENGTH || !IsAsciiHexString(value: trimmed))
                {
                        throw new DomainValidationException(
                                message: $"SHA-256 hash must be exactly {DomainConstants.SHA256_HEX_LENGTH} hexadecimal characters.");
                }

                return trimmed.ToLowerInvariant();
        }

        private static bool IsAsciiHexString(string value)
        {
                foreach (char character in value)
                {
                        if (!char.IsAsciiHexDigit(c: character))
                        {
                                return false;
                        }
                }

                return true;
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
