using ContentService.Application.Common;
using ContentService.Domain.Common;

namespace ContentService.Application.Options;

/// <summary>
/// Configures storage node identity, upload byte limits, disk quota budget, and health alert thresholds for ContentService.
/// </summary>
/// <remarks>
/// Bound via <c>IOptions&lt;ContentStorageOptions&gt;</c> so operators can tune node capacity per deployment without recompiling domain limits.
/// </remarks>
public sealed class ContentStorageOptions
{
        /// <summary>
        /// Gets or sets the storage node (shard) identifier assigned to newly created images and templates.
        /// </summary>
        public string StorageNodeId { get; set; } = ApplicationConstants.DEFAULT_STORAGE_NODE_ID;

        /// <summary>
        /// Gets or sets the optional public HTTP base URL advertised by this node in Dapr pool offers (e.g., <c>http://localhost:5101</c>).
        /// </summary>
        public string? PublicBaseUrl { get; set; }

        /// <summary>
        /// Gets or sets the Dapr Pub/Sub component name used for stateless pool scatter-gather broadcasts.
        /// </summary>
        public string PubSubName { get; set; } = ApplicationConstants.DEFAULT_PUBSUB_NAME;

        /// <summary>
        /// Gets or sets the timeout in seconds when waiting for peer nodes to respond to a pool broadcast.
        /// </summary>
        public int PoolRequestTimeoutSeconds { get; set; } = ApplicationConstants.DEFAULT_POOL_REQUEST_TIMEOUT_SECONDS;

        /// <summary>
        /// Gets or sets optional peer storage node identifiers for direct checks (e.g., cross-node delete reference checks).
        /// </summary>
        public IReadOnlyList<string> PeerStorageNodeIds { get; set; } = [];

        /// <summary>
        /// Gets or sets the maximum permitted image upload size in bytes, bounded by <see cref="DomainConstants.MAX_IMAGE_SIZE_BYTES"/>.
        /// </summary>
        public long MaxImageSizeBytes { get; set; } = DomainConstants.MAX_IMAGE_SIZE_BYTES;

        /// <summary>
        /// Gets or sets the total storage byte quota across stored image binaries on this storage node.
        /// </summary>
        public long TotalQuotaBytes { get; set; } = ApplicationConstants.DEFAULT_TOTAL_QUOTA_BYTES;

        /// <summary>
        /// Gets or sets the storage utilization percentage at or above which health transitions to Warning.
        /// </summary>
        public double WarningThresholdPercent { get; set; } = ApplicationConstants.DEFAULT_WARNING_THRESHOLD_PERCENT;

        /// <summary>
        /// Gets or sets the storage utilization percentage at or above which health transitions to FullReadOnly.
        /// </summary>
        public double CriticalThresholdPercent { get; set; } = ApplicationConstants.DEFAULT_CRITICAL_THRESHOLD_PERCENT;
}

