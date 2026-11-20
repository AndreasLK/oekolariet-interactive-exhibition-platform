using ContentService.Domain.Common;

namespace ContentService.Application.Common;

/// <summary>
/// Centralizes application-layer defaults, pagination bounds, stream buffer sizing, and configuration section constants.
/// </summary>
/// <remarks>
/// Size and MIME rules live in <see cref="DomainConstants"/>; this class holds orchestration-level defaults that do not belong in the domain model.
/// </remarks>
public static class ApplicationConstants
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
        /// Number of bytes in one binary gibibyte (1,073,741,824 bytes).
        /// </summary>
        private const long BYTES_PER_GIBIBYTE = (long)BYTES_PER_MEBIBYTE * BYTES_PER_KIBIBYTE;

        /// <summary>
        /// Buffer size in kibibytes matching .NET's default Stream.CopyToAsync buffer while remaining safely below the 85 KiB Large Object Heap threshold.
        /// </summary>
        private const int STREAM_BUFFER_KIBIBYTES = 80;

        /// <summary>
        /// Default storage node quota in gibibytes when no explicit configuration override is supplied.
        /// </summary>
        private const int DEFAULT_TOTAL_QUOTA_GIBIBYTES = 5;

        /// <summary>
        /// Default 1-based page number applied when callers omit or pass a non-positive page index.
        /// </summary>
        public const int DEFAULT_PAGE = 1;

        /// <summary>
        /// Minimum allowed number of items returned in a single page.
        /// </summary>
        public const int MIN_PAGE_SIZE = 1;

        /// <summary>
        /// Default number of items returned per page when callers pass an out-of-range page size.
        /// </summary>
        public const int DEFAULT_PAGE_SIZE = 25;

        /// <summary>
        /// Upper bound on items per page to protect database queries and JSON payloads from unbounded reads.
        /// </summary>
        public const int MAX_PAGE_SIZE = 100;

        /// <summary>
        /// Stream copy and incremental hashing buffer size in bytes (81,920 bytes).
        /// </summary>
        /// <remarks>
        /// Uses 80 KiB so pooled buffers stay on the Small Object Heap instead of causing LOH fragmentation.
        /// </remarks>
        public const int STREAM_BUFFER_SIZE_BYTES = STREAM_BUFFER_KIBIBYTES * BYTES_PER_KIBIBYTE;

        /// <summary>
        /// Minimum leading byte slice length required to inspect all supported raster image magic headers (PNG, JPEG, WebP).
        /// </summary>
        public const int MAX_MAGIC_HEADER_LENGTH = 12;

        /// <summary>
        /// Maximum allowed character length for offline exhibition implementation JSON payloads.
        /// </summary>
        /// <remarks>
        /// Aligns with <see cref="DomainConstants.MAX_SCHEMA_JSON_LENGTH"/> so publish-bundle scanning maintains predictable CPU and memory bounds.
        /// </remarks>
        public const int MAX_IMPLEMENTATION_JSON_LENGTH = DomainConstants.MAX_SCHEMA_JSON_LENGTH;

        /// <summary>
        /// Configuration section key used to bind <see cref="Options.ContentStorageOptions"/>.
        /// </summary>
        public const string CONTENT_STORAGE_SECTION_NAME = "ContentStorage";

        /// <summary>
        /// Default storage node (shard) identifier used when configuring a single-node installation.
        /// </summary>
        public const string DEFAULT_STORAGE_NODE_ID = "node-1";

        /// <summary>
        /// Default total byte budget across stored image binaries on a single storage node (5 GiB).
        /// </summary>
        public const long DEFAULT_TOTAL_QUOTA_BYTES = DEFAULT_TOTAL_QUOTA_GIBIBYTES * BYTES_PER_GIBIBYTE;

        /// <summary>
        /// Lower bound (inclusive) for percentage-based storage health thresholds.
        /// </summary>
        public const double MIN_THRESHOLD_PERCENT = 0.0;

        /// <summary>
        /// Default utilization percentage at which storage health transitions to <see cref="Domain.Enums.StorageHealthStatus.Warning"/>.
        /// </summary>
        public const double DEFAULT_WARNING_THRESHOLD_PERCENT = 80.0;

        /// <summary>
        /// Default utilization percentage at which storage health transitions to <see cref="Domain.Enums.StorageHealthStatus.FullReadOnly"/>.
        /// </summary>
        public const double DEFAULT_CRITICAL_THRESHOLD_PERCENT = 95.0;

        /// <summary>
        /// Upper bound (inclusive) for percentage-based storage health thresholds.
        /// </summary>
        public const double MAX_THRESHOLD_PERCENT = 100.0;

        /// <summary>
        /// Utilization percentage reported when a storage node is configured with a zero or non-positive byte quota.
        /// </summary>
        public const double FULL_UTILIZATION_PERCENT = 100.0;

        /// <summary>
        /// Exact decimal multiplier used to convert a used-to-quota ratio into a percentage without IEEE-754 rounding drift.
        /// </summary>
        public const decimal PERCENT_MULTIPLIER = 100m;

        /// <summary>
        /// RFC 9110 weak ETag prefix stripped when matching conditional <c>If-None-Match</c> request headers.
        /// </summary>
        public const string WEAK_ETAG_PREFIX = "W/";

        /// <summary>
        /// Quotation character enclosing HTTP ETag header tokens.
        /// </summary>
        public const char ETAG_QUOTE_CHAR = '"';

        /// <summary>
        /// Delimiter separating multiple candidate entity tags in an <c>If-None-Match</c> header value.
        /// </summary>
        public const char ETAG_LIST_SEPARATOR = ',';

        /// <summary>
        /// Minimum string length required for a quoted token (<c>""</c>) before stripping surrounding quote characters.
        /// </summary>
        public const int MINIMUM_QUOTED_ETAG_LENGTH = 2;

        /// <summary>
        /// Field delimiter used when serializing deterministic asset records into offline publish-bundle checksums.
        /// </summary>
        public const char CHECKSUM_FIELD_SEPARATOR = ':';

        /// <summary>
        /// Field label included in template schema JSON validation error messages.
        /// </summary>
        public const string SCHEMA_JSON_FIELD_NAME = "Schema JSON";

        /// <summary>
        /// Field label included in exhibition implementation JSON validation error messages.
        /// </summary>
        public const string IMPLEMENTATION_JSON_FIELD_NAME = "Implementation JSON";
}

