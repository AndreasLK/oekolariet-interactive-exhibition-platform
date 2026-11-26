namespace ContentService.Domain.Common;

/// <summary>
/// Centralizes compile-time limits, formatting rules, and security allowlists for ContentService.
/// </summary>
// Kept as static compile-time constants rather than IOptions<T> so entities and value objects can validate rules without DI.
public static class DomainConstants
{
        /// <summary>
        /// Number of bytes in one binary kibibyte (1,024 bytes).
        /// </summary>
        private const int BYTES_PER_KIBIBYTE = 1024;

        /// <summary>
        /// Number of bytes in one binary mebibyte (1,048,576 bytes).
        /// </summary>
        private const int BYTES_PER_MEBIBYTE = BYTES_PER_KIBIBYTE * BYTES_PER_KIBIBYTE;

        /// <summary>
        /// Maximum allowed image upload size in mebibytes.
        /// </summary>
        // Capped at 10 MiB so image uploads and kiosk cache syncs stay fast over local museum networks.
        public const int MAX_IMAGE_SIZE_MEGABYTES = 10;

        /// <summary>
        /// Maximum allowed template schema JSON size in kibibytes.
        /// </summary>
        // Capped at 512 KiB so regex scanning and database reads stay predictable without streaming.
        public const int MAX_SCHEMA_JSON_KILOBYTES = 512;

        /// <summary>
        /// Maximum character length permitted for stored image file names.
        /// </summary>
        // Matches standard OS filename limits (255 chars) so kiosk disk caches do not hit path length errors.
        public const int MAX_FILE_NAME_LENGTH = 255;

        /// <summary>
        /// Maximum character length permitted for template display names.
        /// </summary>
        public const int MAX_TEMPLATE_NAME_LENGTH = 100;

        /// <summary>
        /// Maximum character length permitted for audit actor identifiers.
        /// </summary>
        // Sized to fit standard OpenID Connect subject claims and service principal IDs in an indexed column.
        public const int MAX_ACTOR_ID_LENGTH = 128;

        /// <summary>
        /// Maximum character length permitted for optional image alternative text.
        /// </summary>
        public const int MAX_ALT_TEXT_LENGTH = 500;

        /// <summary>
        /// Maximum character length permitted for a normalized recursive album path (e.g., <c>Madaffald/Frugt</c>).
        /// </summary>
        public const int MAX_ALBUM_PATH_LENGTH = 256;

        /// <summary>
        /// Maximum character length permitted for a single album segment name.
        /// </summary>
        public const int MAX_ALBUM_SEGMENT_LENGTH = 64;

        /// <summary>
        /// Maximum nested folder depth permitted in a recursive album path.
        /// </summary>
        public const int MAX_ALBUM_DEPTH = 8;

        /// <summary>
        /// Canonical slash separator between album hierarchy segments.
        /// </summary>
        public const char ALBUM_PATH_SEPARATOR = '/';

        /// <summary>
        /// Maximum character length permitted for normalized comma-separated image tags.
        /// </summary>
        public const int MAX_IMAGE_TAGS_LENGTH = 500;

        /// <summary>
        /// Maximum number of distinct tags allowed per image.
        /// </summary>
        public const int MAX_IMAGE_TAG_COUNT = 20;

        /// <summary>
        /// Maximum character length permitted for a single normalized tag token.
        /// </summary>
        public const int MAX_SINGLE_TAG_LENGTH = 40;

        /// <summary>
        /// Canonical comma delimiter used to store normalized tags on <c>ImageEntity</c>.
        /// </summary>
        public const char IMAGE_TAG_SEPARATOR = ',';

        /// <summary>
        /// Maximum character length permitted for a storage node (shard) identifier.
        /// </summary>
        // Kept short because the node ID is embedded inside composite '{node}_{guid}' external IDs and 'picture.{node}.{guid}' handles.
        public const int MAX_STORAGE_NODE_ID_LENGTH = 32;

        /// <summary>
        /// Maximum allowed image upload size in bytes.
        /// </summary>
        // Cast to long before multiplying so larger limits above 2047 MiB cannot overflow 32-bit int math.
        public const long MAX_IMAGE_SIZE_BYTES = (long)MAX_IMAGE_SIZE_MEGABYTES * BYTES_PER_MEBIBYTE;

        /// <summary>
        /// Maximum allowed template schema JSON length in UTF-16 characters.
        /// </summary>
        // Uses string.Length as a simple upper-bound check instead of encoding to UTF-8 on every validation call.
        public const int MAX_SCHEMA_JSON_LENGTH = MAX_SCHEMA_JSON_KILOBYTES * BYTES_PER_KIBIBYTE;

        /// <summary>
        /// Exact hexadecimal string length of a 256-bit SHA-256 digest.
        /// </summary>
        // SHA-256 (64 hex chars) avoids MD5 collision risks while staying half the width of SHA-512.
        public const int SHA256_HEX_LENGTH = 64;

        /// <summary>
        /// Initial schema version assigned when a template is created.
        /// </summary>
        // Starts at 1 so an uninitialized 0 value is clearly distinguishable from a saved template.
        public const int INITIAL_VERSION = 1;

        /// <summary>
        /// Minimum allowed image binary payload size in bytes (rejects empty zero-byte uploads).
        /// </summary>
        public const int MIN_IMAGE_SIZE_BYTES = 1;

        /// <summary>
        /// Exact character length of a standard hyphenated GUID formatted with <see cref="GUID_FORMAT_HYPHENATED"/>.
        /// </summary>
        public const int HYPHENATED_GUID_LENGTH = 36;

        /// <summary>
        /// Standard .NET GUID format specifier producing 32 hexadecimal digits separated by hyphens.
        /// </summary>
        // Hyphenated "D" format is used instead of compact "N" so IDs stay readable in logs and JSON schemas.
        public const string GUID_FORMAT_HYPHENATED = "D";

        /// <summary>
        /// Separator placed between storage node ID and GUID in external asset identifiers (<c>{node}_{guid}</c>).
        /// </summary>
        // Uses '_' instead of '.' so HTTP route segments and cached filenames do not gain extra dot-separated parts.
        public const char EXTERNAL_IMAGE_ID_SEPARATOR = '_';

        /// <summary>
        /// Segment delimiter used inside structured picture reference handles (<c>picture.{node}.{guid}</c>).
        /// </summary>
        public const char PICTURE_REFERENCE_SEGMENT_SEPARATOR = '.';

        /// <summary>
        /// Prefix identifying a managed picture reference handle inside template JSON schemas.
        /// </summary>
        // Distinguishes managed image handles from arbitrary strings or URLs when scanning template JSON.
        public const string PICTURE_REFERENCE_PREFIX = "picture.";

        /// <summary>
        /// Unanchored regex pattern for a valid storage node identifier (lowercase alphanumeric segments separated by single hyphens).
        /// </summary>
        // Kept unanchored so PictureReference can reuse this single source of truth both standalone (^...$) and embedded inside template JSON handles.
        // Disallowing underscores and dots prevents ambiguity inside '{node}_{guid}' external IDs and 'picture.{node}.{guid}' handles.
        public const string STORAGE_NODE_ID_PATTERN = @"[a-z0-9]+(?:-[a-z0-9]+)*";

        /// <summary>
        /// Unescaped base64 image data URI prefix disallowed in template JSON schemas.
        /// </summary>
        // Blocking inline data URIs forces templates to reference managed ImageEntity assets instead of bloating database rows and bypassing HTTP caching.
        public const string EMBEDDED_BASE64_DATA_URI_MARKER = "data:image/";

        /// <summary>
        /// JSON-escaped base64 image data URI prefix disallowed in template JSON schemas.
        /// </summary>
        // JSON serializers often escape '/' as '\/', so we check both forms to prevent smuggling inline images past validation.
        public const string ESCAPED_EMBEDDED_BASE64_DATA_URI_MARKER = @"data:image\/";

        /// <summary>
        /// Canonical MIME content type for PNG images.
        /// </summary>
        public const string CONTENT_TYPE_PNG = "image/png";

        /// <summary>
        /// Canonical MIME content type for JPEG images.
        /// </summary>
        public const string CONTENT_TYPE_JPEG = "image/jpeg";

        /// <summary>
        /// Canonical MIME content type for WebP images.
        /// </summary>
        public const string CONTENT_TYPE_WEBP = "image/webp";

        /// <summary>
        /// Allowlist of raster image MIME content types accepted by ContentService.
        /// </summary>
        // SVG is intentionally excluded because SVGs are XML files that can contain executable <script> tags (stored XSS risk).
        public static readonly IReadOnlySet<string> ALLOWED_IMAGE_CONTENT_TYPES = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
                CONTENT_TYPE_PNG,
                CONTENT_TYPE_JPEG,
                CONTENT_TYPE_WEBP
        };

        /// <summary>
        /// Path-related substrings forbidden inside uploaded image file names.
        /// </summary>
        // Prevents path-traversal attacks (e.g., '../../secret.png') if kiosk caches build local file paths from image metadata.
        public static readonly IReadOnlyList<string> FORBIDDEN_FILE_NAME_SEGMENTS = ["/", "\\", ".."];
}
