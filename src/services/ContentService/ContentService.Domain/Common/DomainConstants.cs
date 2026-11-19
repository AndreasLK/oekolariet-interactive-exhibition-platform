using System.Collections.Frozen;

namespace ContentService.Domain.Common;

/// <summary>
/// Provides domain-wide constants and validation limits for the Content Service.
/// </summary>
public static class DomainConstants
{
        /// <summary>
        /// Number of bytes in one kilobyte.
        /// </summary>
        public const int BYTES_PER_KILOBYTE = 1024;

        /// <summary>
        /// Number of bytes in one megabyte.
        /// </summary>
        public const int BYTES_PER_MEGABYTE = BYTES_PER_KILOBYTE * BYTES_PER_KILOBYTE;

        /// <summary>
        /// Maximum allowed image file size in megabytes.
        /// </summary>
        public const int MAX_IMAGE_SIZE_MEGABYTES = 10;

        /// <summary>
        /// Maximum allowed template schema JSON size in kilobytes.
        /// </summary>
        public const int MAX_SCHEMA_JSON_KILOBYTES = 512;

        /// <summary>
        /// Maximum allowed character length for an image file name.
        /// </summary>
        public const int MAX_FILE_NAME_LENGTH = 255;

        /// <summary>
        /// Maximum allowed character length for a template name.
        /// </summary>
        public const int MAX_TEMPLATE_NAME_LENGTH = 100;

        /// <summary>
        /// Maximum allowed character length for a draft title.
        /// </summary>
        public const int MAX_DRAFT_TITLE_LENGTH = 150;

        /// <summary>
        /// Maximum allowed character length for an actor identifier.
        /// </summary>
        public const int MAX_ACTOR_ID_LENGTH = 128;

        /// <summary>
        /// Maximum allowed character length for image alternative text.
        /// </summary>
        public const int MAX_ALT_TEXT_LENGTH = 500;

        /// <summary>
        /// Maximum allowed image file size in bytes.
        /// </summary>
        public const long MAX_IMAGE_SIZE_BYTES = MAX_IMAGE_SIZE_MEGABYTES * BYTES_PER_MEGABYTE;

        /// <summary>
        /// Maximum allowed template schema JSON length in characters.
        /// </summary>
        public const int MAX_SCHEMA_JSON_LENGTH = MAX_SCHEMA_JSON_KILOBYTES * BYTES_PER_KILOBYTE;

        /// <summary>
        /// Maximum allowed draft configuration JSON length in characters.
        /// </summary>
        public const int MAX_CONFIGURATION_JSON_LENGTH = BYTES_PER_MEGABYTE;

        /// <summary>
        /// Expected hexadecimal character length of a SHA-256 hash.
        /// </summary>
        public const int SHA256_HEX_LENGTH = 64;

        /// <summary>
        /// Initial version number assigned to newly created templates and published drafts.
        /// </summary>
        public const int INITIAL_VERSION = 1;

        /// <summary>
        /// Minimum allowed image file size in bytes.
        /// </summary>
        public const int MIN_IMAGE_SIZE_BYTES = 1;

        /// <summary>
        /// Character length of a hyphenated GUID formatted with the "D" specifier.
        /// </summary>
        public const int GUID_D_LENGTH = 36;

        /// <summary>
        /// Standard hyphenated GUID format specifier.
        /// </summary>
        public const string GUID_FORMAT_HYPHENATED = "D";

        /// <summary>
        /// Prefix used for picture handle references in JSON payloads.
        /// </summary>
        public const string PICTURE_HANDLE_PREFIX = "picture.";

        /// <summary>
        /// Unescaped base64 image data URI marker disallowed in JSON payloads.
        /// </summary>
        public const string EMBEDDED_BASE64_DATA_URI_MARKER = "data:image/";

        /// <summary>
        /// JSON-escaped base64 image data URI marker disallowed in JSON payloads.
        /// </summary>
        public const string ESCAPED_EMBEDDED_BASE64_DATA_URI_MARKER = @"data:image\/";

        /// <summary>
        /// MIME content type for PNG images.
        /// </summary>
        public const string CONTENT_TYPE_PNG = "image/png";

        /// <summary>
        /// MIME content type for JPEG images.
        /// </summary>
        public const string CONTENT_TYPE_JPEG = "image/jpeg";

        /// <summary>
        /// MIME content type for WebP images.
        /// </summary>
        public const string CONTENT_TYPE_WEBP = "image/webp";

        // SVG is excluded to prevent stored XSS payloads (OWASP).
        /// <summary>
        /// Set of MIME content types permitted for uploaded images.
        /// </summary>
        public static readonly IReadOnlySet<string> ALLOWED_IMAGE_CONTENT_TYPES = FrozenSet.Create(
                StringComparer.OrdinalIgnoreCase,
                CONTENT_TYPE_PNG,
                CONTENT_TYPE_JPEG,
                CONTENT_TYPE_WEBP);
}
