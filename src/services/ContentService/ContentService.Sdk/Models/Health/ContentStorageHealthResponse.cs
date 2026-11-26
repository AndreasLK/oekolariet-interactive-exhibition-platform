namespace ContentService.Sdk.Models;

/// <summary>
/// Immutable response reporting storage node usage, quota limits, and capacity health status.
/// </summary>
public sealed record class ContentStorageHealthResponse
{
        /// <summary>
        /// Gets the current capacity health status of the storage node.
        /// </summary>
        public required ContentStorageHealthStatus Status { get; init; }

        /// <summary>
        /// Gets the current storage consumption in bytes.
        /// </summary>
        public required long UsedBytes { get; init; }

        /// <summary>
        /// Gets the configured maximum storage quota in bytes.
        /// </summary>
        public required long QuotaBytes { get; init; }

        /// <summary>
        /// Gets the computed quota utilization percentage.
        /// </summary>
        public required double UtilizationPercent { get; init; }
}

