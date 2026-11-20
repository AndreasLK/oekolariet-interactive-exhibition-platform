namespace ContentService.Domain.Enums;

/// <summary>
/// Represents the disk capacity health state of a ContentService storage node.
/// </summary>
// Explicit integer backing values are used instead of string storage or a smart-enum class so EF Core and MSSQL rows stay compact and immune to member reordering without migration churn.
public enum StorageHealthStatus
{
        /// <summary>
        /// Storage usage is below the configured warning threshold and accepts normal read and write operations.
        /// </summary>
        Healthy = 0,

        /// <summary>
        /// Storage usage has reached the warning threshold, signaling IT to provision an additional storage node before capacity is exhausted.
        /// </summary>
        // Uses a distinct warning state before FullReadOnly so operators have lead time to spin up a new self-contained shard.
        Warning = 1,

        /// <summary>
        /// Storage capacity is exhausted; the node rejects new uploads while continuing to serve existing assets in read-only mode.
        /// </summary>
        // Transitions a full shard to read-only instead of failing unpredictably, keeping active exhibition kiosks serving previously uploaded assets.
        FullReadOnly = 2
}
