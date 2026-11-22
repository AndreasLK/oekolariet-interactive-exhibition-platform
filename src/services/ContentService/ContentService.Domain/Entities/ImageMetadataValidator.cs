using ContentService.Domain.Common;
using ContentService.Domain.Exceptions;

namespace ContentService.Domain.Entities;

/// <summary>
/// Centralizes stateless metadata validation and normalization rules for <see cref="ImageEntity"/>.
/// </summary>
internal static class ImageMetadataValidator
{
        /// <summary>
        /// Validates, trims, and checks an uploaded file name for length and path-traversal safety.
        /// </summary>
        /// <param name="fileName">The raw file name supplied by the caller.</param>
        /// <returns>The trimmed, safe file name.</returns>
        /// <exception cref="DomainValidationException">Thrown when <paramref name="fileName"/> is blank, exceeds <see cref="DomainConstants.MAX_FILE_NAME_LENGTH"/>, or contains forbidden path characters.</exception>
        public static string ValidateFileName(string fileName)
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

                if (ContainsForbiddenPathCharacters(fileName: trimmed))
                {
                        throw new DomainValidationException(
                                "File name cannot contain path separators, parent-directory traversal segments, or control characters.");
                }

                return trimmed;
        }

        /// <summary>
        /// Validates and normalizes an image MIME content type against the permitted raster allowlist.
        /// </summary>
        /// <param name="contentType">The raw MIME content type.</param>
        /// <returns>The trimmed, lowercase MIME content type.</returns>
        /// <exception cref="DomainValidationException">Thrown when <paramref name="contentType"/> is blank or not in <see cref="DomainConstants.ALLOWED_IMAGE_CONTENT_TYPES"/>.</exception>
        public static string ValidateContentType(string contentType)
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
        public static void ValidateFileSize(long fileSizeBytes)
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
        public static string ValidateSha256Hash(string sha256Hash)
        {
                if (string.IsNullOrWhiteSpace(sha256Hash))
                {
                        throw new DomainValidationException("SHA-256 hash cannot be null or whitespace.");
                }

                string trimmed = sha256Hash.Trim();

                // Strict 64-char hex validation prevents malformed strings from reaching HTTP ETag headers or breaking cache deduplication.
                if (trimmed.Length != DomainConstants.SHA256_HEX_LENGTH || !IsAsciiHexString(value: trimmed))
                {
                        throw new DomainValidationException(
                                $"SHA-256 hash must be exactly {DomainConstants.SHA256_HEX_LENGTH} hexadecimal characters.");
                }

                return trimmed.ToLowerInvariant();
        }

        /// <summary>
        /// Validates and normalizes optional accessibility alternative text.
        /// </summary>
        /// <param name="altText">The raw alternative text, or <see langword="null"/>.</param>
        /// <returns>The trimmed alternative text, or <see langword="null"/> when input is blank.</returns>
        /// <exception cref="DomainValidationException">Thrown when the trimmed alternative text exceeds <see cref="DomainConstants.MAX_ALT_TEXT_LENGTH"/>.</exception>
        public static string? ValidateAltText(string? altText)
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

        /// <summary>
        /// Detects path separators, parent-directory traversal sequences (<c>..</c>), and control characters inside a file name.
        /// </summary>
        private static bool ContainsForbiddenPathCharacters(string fileName)
        {
                // Rejects any '..' substring (even rare names like 'photo..png') to guarantee simple path-traversal protection if kiosk caches write files to disk.
                foreach (string forbiddenSegment in DomainConstants.FORBIDDEN_FILE_NAME_SEGMENTS)
                {
                        if (fileName.Contains(value: forbiddenSegment, comparisonType: StringComparison.Ordinal))
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
        /// Checks whether every character in a string is an ASCII hexadecimal digit (<c>0-9</c>, <c>a-f</c>, <c>A-F</c>).
        /// </summary>
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
}

