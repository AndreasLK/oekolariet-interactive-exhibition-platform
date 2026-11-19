using System.Collections.Frozen;

namespace ContentService.Domain.Common;

public static class DomainConstants
{
        public const int BYTES_PER_KILOBYTE = 1024;

        public const int BYTES_PER_MEGABYTE = BYTES_PER_KILOBYTE * BYTES_PER_KILOBYTE;

        public const int MAX_IMAGE_SIZE_MEGABYTES = 10;

        public const int MAX_SCHEMA_JSON_KILOBYTES = 512;

        public const int MAX_FILE_NAME_LENGTH = 255;

        public const int MAX_TEMPLATE_NAME_LENGTH = 100;

        public const int MAX_ACTOR_ID_LENGTH = 128;

        public const int MAX_ALT_TEXT_LENGTH = 500;

        public const int MAX_NODE_ID_LENGTH = 32;

        public const long MAX_IMAGE_SIZE_BYTES = MAX_IMAGE_SIZE_MEGABYTES * BYTES_PER_MEGABYTE;

        public const int MAX_SCHEMA_JSON_LENGTH = MAX_SCHEMA_JSON_KILOBYTES * BYTES_PER_KILOBYTE;

        public const int SHA256_HEX_LENGTH = 64;

        public const int INITIAL_VERSION = 1;

        public const int MIN_IMAGE_SIZE_BYTES = 1;

        public const int GUID_D_LENGTH = 36;

        public const string GUID_FORMAT_HYPHENATED = "D";

        // Warns staff/IT early enough to provision another self-contained ContentService + MSSQL node or expand disk before hitting capacity.
        public const int DEFAULT_WARNING_THRESHOLD_PERCENT = 90;

        public const int FULL_CAPACITY_PERCENT = 100;

        public const char ASSET_ID_SEPARATOR = '_';

        public const char PICTURE_HANDLE_SEGMENT_SEPARATOR = '.';

        public const string PICTURE_HANDLE_PREFIX = "picture.";

        // Restricts node IDs to lowercase URL- and separator-safe tokens so external IDs ({nodeId}_{guid}) and handles (picture.{nodeId}.{guid}) can be routed unambiguously by the Content Gateway.
        public const string NODE_ID_VALIDATION_PATTERN = @"^[a-z0-9]+(?:-[a-z0-9]+)*$";

        // JSON serializers frequently escape forward slashes as \/; checking both prevents smuggling raw Base64 image blobs past the guard, which would bloat MSSQL rows and break HTTP caching.
        public const string EMBEDDED_BASE64_DATA_URI_MARKER = "data:image/";

        public const string ESCAPED_EMBEDDED_BASE64_DATA_URI_MARKER = @"data:image\/";

        public const string CONTENT_TYPE_PNG = "image/png";

        public const string CONTENT_TYPE_JPEG = "image/jpeg";

        public const string CONTENT_TYPE_WEBP = "image/webp";

        // OWASP stored-XSS defense: SVG is explicitly excluded because SVG is XML that can carry executable script payloads.
        public static readonly IReadOnlySet<string> ALLOWED_IMAGE_CONTENT_TYPES = FrozenSet.Create(
                StringComparer.OrdinalIgnoreCase,
                CONTENT_TYPE_PNG,
                CONTENT_TYPE_JPEG,
                CONTENT_TYPE_WEBP);

        // OWASP path-traversal defense preventing crafted filenames from escaping cache directories if downstream kiosk servers derive file paths from metadata.
        public static readonly IReadOnlyList<string> FORBIDDEN_FILE_NAME_SEGMENTS = ["/", "\\", ".."];
}
