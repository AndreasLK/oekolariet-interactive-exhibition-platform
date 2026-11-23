using ContentService.Domain.Common;

namespace ContentService.Application.Common;

/// <summary>
/// Centralizes application-layer defaults so use cases share consistent paging bounds, buffer sizing, and quota thresholds without polluting domain entities.
/// </summary>
/// <remarks>
/// Size and MIME invariants remain in <see cref="DomainConstants"/> because they are business rules, whereas these defaults govern transport, memory, and policy configuration.
/// </remarks>
public static class ApplicationConstants
{
        /// <summary>
        /// Binary kibibyte factor (<c>1 KiB = 1024 bytes</c>, binary base-2 unit vs decimal <c>1000</c>-byte KB).
        /// </summary>
        private const int BYTES_PER_KIBIBYTE = 1024;

        /// <summary>
        /// Binary mebibyte factor (<c>1 MiB = 1024 KiB</c>).
        /// </summary>
        private const int BYTES_PER_MEBIBYTE = BYTES_PER_KIBIBYTE * BYTES_PER_KIBIBYTE;

        /// <summary>
        /// Binary gibibyte factor (<c>1 GiB = 1024 MiB</c>) using <see langword="long"/> to avoid integer overflow in quota math.
        /// </summary>
        private const long BYTES_PER_GIBIBYTE = (long)BYTES_PER_MEBIBYTE * BYTES_PER_KIBIBYTE;

        /// <summary>
        /// Keeps upload copy buffers at 80 KiB (81,920 bytes) so each chunk array stays safely below .NET's 85,000-byte Large Object Heap (LOH) threshold while matching <see cref="Stream.CopyTo(Stream)"/>'s default efficiency.
        /// </summary>
        /// <remarks>
        /// Allocations of 85,000 bytes or more go straight to the LOH, which is only collected during expensive Gen 2 GCs and is not compacted by default.
        /// Staying at 80 KiB keeps temporary stream buffers on the Small Object Heap (SOH) for cheap Gen 0/1 collection under concurrent uploads.
        /// </remarks>
        private const int STREAM_BUFFER_KIBIBYTES = 80;

        /// <summary>
        /// Default storage budget (5 GiB) so unconfigured dev/test environments still enforce bounded disk usage.
        /// </summary>
        private const int DEFAULT_TOTAL_QUOTA_GIBIBYTES = 5;

        /// <summary>
        /// Normalizes omitted or non-positive page requests to page 1 so pagination math never emits invalid negative SQL offsets.
        /// </summary>
        public const int DEFAULT_PAGE = 1;

        /// <summary>
        /// Prevents zero or negative page sizes from returning empty pages or causing division issues in paging metadata.
        /// </summary>
        public const int MIN_PAGE_SIZE = 1;

        /// <summary>
        /// Balances UI responsiveness with query cost when clients browse catalogs without specifying an explicit page size.
        /// </summary>
        public const int DEFAULT_PAGE_SIZE = 25;

        /// <summary>
        /// Caps page size to protect SQL Server queries and Blazor WASM JSON deserialization from unbounded memory spikes.
        /// </summary>
        public const int MAX_PAGE_SIZE = 100;

        /// <summary>
        /// Bounds upload chunk buffers to 80 KiB so concurrent image uploads avoid large buffer allocations.
        /// </summary>
        public const int STREAM_BUFFER_SIZE_BYTES = STREAM_BUFFER_KIBIBYTES * BYTES_PER_KIBIBYTE;

        /// <summary>
        /// Mirrors <see cref="DomainConstants.MAX_SCHEMA_JSON_LENGTH"/> so offline bundle scanning preserves predictable CPU and memory bounds during publish resolution.
        /// </summary>
        public const int MAX_IMPLEMENTATION_JSON_LENGTH = DomainConstants.MAX_SCHEMA_JSON_LENGTH;

        /// <summary>
        /// Configuration section key decoupling <see cref="Options.ContentStorageOptions"/> binding from literal strings across startup and tests.
        /// </summary>
        public const string CONTENT_STORAGE_SECTION_NAME = "ContentStorage";

        /// <summary>
        /// Logical storage-instance label attached to uploaded assets (defaults to <c>"node-1"</c>; everything currently runs in one SQL Server database, while keeping picture references ready if storage is ever split across multiple nodes).
        /// </summary>
        public const string DEFAULT_STORAGE_NODE_ID = "node-1";

        /// <summary>
        /// Provides a fallback 5 GiB quota when configuration omits <c>TotalQuotaBytes</c>, preventing accidental unbounded image growth.
        /// </summary>
        public const long DEFAULT_TOTAL_QUOTA_BYTES = DEFAULT_TOTAL_QUOTA_GIBIBYTES * BYTES_PER_GIBIBYTE;

        /// <summary>
        /// Lower validation bound ensuring configured warning/critical quota percentages cannot be negative.
        /// </summary>
        public const double MIN_THRESHOLD_PERCENT = 0.0;

        /// <summary>
        /// Gives operators early warning at 80% capacity so storage can be cleaned up before uploads start failing.
        /// </summary>
        public const double DEFAULT_WARNING_THRESHOLD_PERCENT = 80.0;

        /// <summary>
        /// Marks storage critical at 95% capacity so monitoring alerts fire before hard 100% exhaustion.
        /// </summary>
        public const double DEFAULT_CRITICAL_THRESHOLD_PERCENT = 95.0;

        /// <summary>
        /// Upper validation bound ensuring configured warning/critical quota percentages stay within 0-100%.
        /// </summary>
        public const double MAX_THRESHOLD_PERCENT = 100.0;

        /// <summary>
        /// Reports 100% utilization when quota is zero or non-positive so health endpoints fail safe instead of dividing by zero.
        /// </summary>
        public const double FULL_UTILIZATION_PERCENT = 100.0;

        /// <summary>
        /// Uses <c>decimal</c> arithmetic for quota percentage math to avoid binary floating-point rounding drift near threshold boundaries.
        /// </summary>
        public const decimal PERCENT_MULTIPLIER = 100m;

        /// <summary>
        /// Separates asset identity fields inside length-prefixed bundle checksum records for readable canonical hashing.
        /// </summary>
        public const char CHECKSUM_FIELD_SEPARATOR = ':';

        /// <summary>
        /// Gives clear field-specific validation messages when template schemas fail JSON or base64 checks.
        /// </summary>
        public const string SCHEMA_JSON_FIELD_NAME = "Schema JSON";

        /// <summary>
        /// Distinguishes exhibition implementation JSON errors from template schema errors in shared validation helpers.
        /// </summary>
        public const string IMPLEMENTATION_JSON_FIELD_NAME = "Implementation JSON";
}
