using ContentService.Domain.Common;
using ContentService.Domain.Exceptions;
using ContentService.Domain.ValueObjects;

namespace ContentService.Domain.Entities;

/// <summary>
/// Represents the metadata, content verification digest, and soft-delete lifecycle of a stored exhibition image.
/// </summary>
// Metadata lives here while raw image bytes live in ImageBinaryEntity so listing or searching images never loads megabytes of pixel data from the database.
public sealed class ImageEntity : BaseEntity
{
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageEntity"/> class for EF Core.
        /// </summary>
        private ImageEntity()
        {
        }

        /// <summary>
        /// Gets the storage node (shard) identifier where this image is persisted.
        /// </summary>
        public string StorageNodeId { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the sanitized original file name of the uploaded image.
        /// </summary>
        public string FileName { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the normalized lowercase MIME content type of the image.
        /// </summary>
        public string ContentType { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the verified byte length of the image payload.
        /// </summary>
        // Stored on metadata so quota checks and API headers do not have to load ImageBinaryEntity.
        public long FileSizeBytes { get; private set; }

        /// <summary>
        /// Gets the lowercase 64-character hexadecimal SHA-256 hash of the image payload.
        /// </summary>
        // Used to detect duplicate uploads and serve HTTP ETag headers so browsers/kiosks can skip re-downloading unchanged images.
        public string Sha256Hash { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the optional accessibility alternative text for the image, or <see langword="null"/> if not provided.
        /// </summary>
        public string? AltText { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this image has been soft-deleted.
        /// </summary>
        // Soft-delete keeps metadata intact so drafts stored in EditorService's separate database do not break when an image is retired.
        public bool IsDeleted { get; private set; }

        /// <summary>
        /// Gets the external route identifier (<c>{node}_{guid}</c>) for this image.
        /// </summary>
        public string ExternalId => this.Reference.ToExternalImageId();

        /// <summary>
        /// Gets the structured <see cref="PictureReference"/> handle (<c>picture.{node}.{guid}</c>) for this image.
        /// </summary>
        // Two formats exist because ExternalId ('_') fits clean HTTP URLs whereas PictureReference ('picture.{node}.{guid}') stands out inside template JSON.
        public PictureReference Reference => new(this.StorageNodeId, this.Id);

        /// <summary>
        /// Creates a validated <see cref="ImageEntity"/> with initialized audit metadata.
        /// </summary>
        /// <param name="storageNodeId">The storage node identifier owning the image.</param>
        /// <param name="fileName">The uploaded image file name.</param>
        /// <param name="contentType">The MIME content type of the image.</param>
        /// <param name="fileSizeBytes">The byte size of the image binary payload.</param>
        /// <param name="sha256Hash">The 64-character hexadecimal SHA-256 digest of the image payload.</param>
        /// <param name="actorId">The identifier of the actor creating the image.</param>
        /// <param name="altText">Optional accessibility alternative text.</param>
        /// <returns>A newly created and validated <see cref="ImageEntity"/>.</returns>
        /// <exception cref="DomainValidationException">Thrown when any metadata field or <paramref name="actorId"/> violates domain invariants.</exception>
        public static ImageEntity Create(
                string storageNodeId,
                string fileName,
                string contentType,
                long fileSizeBytes,
                string sha256Hash,
                string actorId,
                string? altText = null)
        {
                // Validate every field before creating the entity so an invalid ImageEntity can never exist in memory.
                string validatedStorageNodeId = PictureReference.ValidateStorageNodeId(storageNodeId);
                string validatedFileName = ValidateFileName(fileName);
                string validatedContentType = ValidateContentType(contentType);
                ValidateFileSize(fileSizeBytes);
                string validatedHash = ValidateSha256Hash(sha256Hash);
                string? validatedAltText = ValidateAltText(altText);

                ImageEntity entity = new()
                {
                        StorageNodeId = validatedStorageNodeId,
                        FileName = validatedFileName,
                        ContentType = validatedContentType,
                        FileSizeBytes = fileSizeBytes,
                        Sha256Hash = validatedHash,
                        AltText = validatedAltText,
                        IsDeleted = false
                };

                entity.InitializeAudit(actorId);
                return entity;
        }

        /// <summary>
        /// Updates the image's optional accessibility alternative text and records modification audit metadata.
        /// </summary>
        /// <param name="altText">The new alternative text, or blank/<see langword="null"/> to clear it.</param>
        /// <param name="actorId">The identifier of the actor performing the update.</param>
        /// <exception cref="DomainValidationException">Thrown when the image is already deleted, <paramref name="altText"/> exceeds the length limit, or <paramref name="actorId"/> is invalid.</exception>
        public void UpdateAltText(string? altText, string actorId)
        {
                if (this.IsDeleted)
                {
                        throw new DomainValidationException("Cannot update alternative text of a deleted image.");
                }

                // Validate both altText and actorId before mutating AltText so failed calls leave the entity unchanged.
                string? validatedAltText = ValidateAltText(altText);
                this.MarkModified(actorId);
                this.AltText = validatedAltText;
        }

        /// <summary>
        /// Marks the image as soft-deleted and records modification audit metadata.
        /// </summary>
        /// <param name="actorId">The identifier of the actor deleting the image.</param>
        /// <exception cref="DomainValidationException">Thrown when the image is already deleted or <paramref name="actorId"/> is invalid.</exception>
        public void MarkDeleted(string actorId)
        {
                if (this.IsDeleted)
                {
                        throw new DomainValidationException("Image is already deleted.");
                }

                // Validate actorId via MarkModified before flipping IsDeleted so invalid calls do not partially mutate state.
                this.MarkModified(actorId);
                this.IsDeleted = true;
        }

        /// <summary>
        /// Validates, trims, and checks an uploaded file name for length and path-traversal safety.
        /// </summary>
        /// <param name="fileName">The raw file name supplied by the caller.</param>
        /// <returns>The trimmed, safe file name.</returns>
        /// <exception cref="DomainValidationException">Thrown when <paramref name="fileName"/> is blank, exceeds <see cref="DomainConstants.MAX_FILE_NAME_LENGTH"/>, or contains forbidden path characters.</exception>
        private static string ValidateFileName(string fileName)
        {
                if (string.IsNullOrWhiteSpace(fileName))
                {
                        throw new DomainValidationException("File name cannot be null or whitespace.");
                }

                string trimmed = fileName.Trim();
                if (trimmed.Length > DomainConstants.MAX_FILE_NAME_LENGTH)
                {
                        throw new DomainValidationException(
                                $"File name cannot exceed {DomainConstants.MAX_FILE_NAME_LENGTH} characters.");
                }

                if (ContainsForbiddenPathCharacters(trimmed))
                {
                        throw new DomainValidationException(
                                "File name cannot contain path separators, parent-directory traversal segments, or control characters.");
                }

                return trimmed;
        }

        /// <summary>
        /// Detects path separators, parent-directory traversal sequences (<c>..</c>), and control characters inside a file name.
        /// </summary>
        /// <param name="fileName">The trimmed file name to inspect.</param>
        /// <returns><see langword="true"/> if any forbidden substring or control character is present; otherwise <see langword="false"/>.</returns>
        private static bool ContainsForbiddenPathCharacters(string fileName)
        {
                // Rejects any '..' substring (even rare names like 'photo..png') to guarantee simple path-traversal protection if kiosk caches write files to disk.
                foreach (string forbiddenSegment in DomainConstants.FORBIDDEN_FILE_NAME_SEGMENTS)
                {
                        if (fileName.Contains(forbiddenSegment, StringComparison.Ordinal))
                        {
                                return true;
                        }
                }

                foreach (char character in fileName)
                {
                        if (char.IsControl(character))
                        {
                                return true;
                        }
                }

                return false;
        }

        /// <summary>
        /// Validates and normalizes an image MIME content type against the permitted raster allowlist.
        /// </summary>
        /// <param name="contentType">The raw MIME content type.</param>
        /// <returns>The trimmed, lowercase MIME content type.</returns>
        /// <exception cref="DomainValidationException">Thrown when <paramref name="contentType"/> is blank or not in <see cref="DomainConstants.ALLOWED_IMAGE_CONTENT_TYPES"/>.</exception>
        private static string ValidateContentType(string contentType)
        {
                if (string.IsNullOrWhiteSpace(contentType))
                {
                        throw new DomainValidationException("Content type cannot be null or whitespace.");
                }

                // Normalize to lowercase so HTTP responses always send consistent MIME strings regardless of client casing.
                string normalized = contentType.Trim().ToLowerInvariant();
                if (!DomainConstants.ALLOWED_IMAGE_CONTENT_TYPES.Contains(normalized))
                {
                        throw new DomainValidationException(
                                $"Content type '{normalized}' is not an allowed image content type.");
                }

                return normalized;
        }

        /// <summary>
        /// Ensures an image payload size falls within configured domain byte limits.
        /// </summary>
        /// <param name="fileSizeBytes">The image payload size in bytes.</param>
        /// <exception cref="DomainValidationException">Thrown when <paramref name="fileSizeBytes"/> is outside the permitted byte range.</exception>
        private static void ValidateFileSize(long fileSizeBytes)
        {
                if (fileSizeBytes < DomainConstants.MIN_IMAGE_SIZE_BYTES || fileSizeBytes > DomainConstants.MAX_IMAGE_SIZE_BYTES)
                {
                        throw new DomainValidationException(
                                $"File size must be between {DomainConstants.MIN_IMAGE_SIZE_BYTES} and {DomainConstants.MAX_IMAGE_SIZE_BYTES} bytes.");
                }
        }

        /// <summary>
        /// Validates and normalizes a hexadecimal SHA-256 digest string.
        /// </summary>
        /// <param name="sha256Hash">The raw SHA-256 hexadecimal digest.</param>
        /// <returns>The trimmed, lowercase 64-character hexadecimal SHA-256 string.</returns>
        /// <exception cref="DomainValidationException">Thrown when <paramref name="sha256Hash"/> is blank, not 64 characters, or contains non-hex characters.</exception>
        private static string ValidateSha256Hash(string sha256Hash)
        {
                if (string.IsNullOrWhiteSpace(sha256Hash))
                {
                        throw new DomainValidationException("SHA-256 hash cannot be null or whitespace.");
                }

                string trimmed = sha256Hash.Trim();

                // Strict 64-char hex validation prevents malformed strings from reaching HTTP ETag headers or breaking cache deduplication.
                if (trimmed.Length != DomainConstants.SHA256_HEX_LENGTH || !IsAsciiHexString(trimmed))
                {
                        throw new DomainValidationException(
                                $"SHA-256 hash must be exactly {DomainConstants.SHA256_HEX_LENGTH} hexadecimal characters.");
                }

                return trimmed.ToLowerInvariant();
        }

        /// <summary>
        /// Checks whether every character in a string is an ASCII hexadecimal digit (<c>0-9</c>, <c>a-f</c>, <c>A-F</c>).
        /// </summary>
        /// <param name="value">The string to check.</param>
        /// <returns><see langword="true"/> if all characters are ASCII hex digits; otherwise <see langword="false"/>.</returns>
        private static bool IsAsciiHexString(string value)
        {
                foreach (char character in value)
                {
                        if (!char.IsAsciiHexDigit(character))
                        {
                                return false;
                        }
                }

                return true;
        }

        /// <summary>
        /// Validates and normalizes optional accessibility alternative text.
        /// </summary>
        /// <param name="altText">The raw alternative text, or <see langword="null"/>.</param>
        /// <returns>The trimmed alternative text, or <see langword="null"/> when input is blank.</returns>
        /// <exception cref="DomainValidationException">Thrown when the trimmed alternative text exceeds <see cref="DomainConstants.MAX_ALT_TEXT_LENGTH"/>.</exception>
        private static string? ValidateAltText(string? altText)
        {
                // Collapse whitespace-only input to null so callers and the database treat missing and blank alt text identically.
                if (string.IsNullOrWhiteSpace(altText))
                {
                        return null;
                }

                string trimmed = altText.Trim();
                if (trimmed.Length > DomainConstants.MAX_ALT_TEXT_LENGTH)
                {
                        throw new DomainValidationException(
                                $"Alternative text cannot exceed {DomainConstants.MAX_ALT_TEXT_LENGTH} characters.");
                }

                return trimmed;
        }
}
