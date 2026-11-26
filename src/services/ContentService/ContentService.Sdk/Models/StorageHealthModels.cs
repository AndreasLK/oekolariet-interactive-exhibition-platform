namespace ContentService.Sdk.Models;

/// <summary>
/// Represents the disk capacity health state of a ContentService storage node.
/// </summary>
public enum ContentStorageHealthStatus
{
        /// <summary>
        /// Storage usage is below the configured warning threshold and accepts normal read and write operations.
        /// </summary>
        Healthy = 0,

        /// <summary>
        /// Storage usage has reached the warning threshold, signaling operators to provision additional capacity.
        /// </summary>
        Warning = 1,

        /// <summary>
        /// Storage capacity is exhausted; the node rejects new uploads while continuing to serve existing assets.
        /// </summary>
        FullReadOnly = 2
}

/// <summary>
/// Reports current disk quota consumption and health status for a ContentService storage node.
/// </summary>
public sealed record class ContentStorageHealthReport
{
        /// <summary>
        /// Gets the evaluated storage health status.
        /// </summary>
        public required ContentStorageHealthStatus Status { get; init; }

        /// <summary>
        /// Gets the non-negative byte usage currently recorded on the storage node.
        /// </summary>
        public required long UsedBytes { get; init; }

        /// <summary>
        /// Gets the configured maximum byte quota for the storage node.
        /// </summary>
        public required long QuotaBytes { get; init; }

        /// <summary>
        /// Gets the computed percentage of quota currently consumed.
        /// </summary>
        public required double UtilizationPercent { get; init; }
}

