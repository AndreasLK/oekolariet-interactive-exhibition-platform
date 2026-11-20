namespace ContentService.Domain.Enums;

// Tracks disk capacity health of a storage node; explicit integer values keep persisted DB rows stable if members are reordered.
public enum StorageHealthStatus
{
        Healthy = 0,
        // Disk usage reached DEFAULT_WARNING_THRESHOLD_PERCENT, signaling IT to provision a new storage node before writes lock.
        Warning = 1,
        // Transitions a full shard to read-only instead of crashing writes unpredictably, keeping existing exhibition kiosks serving previously uploaded assets while IT adds a new node.
        FullReadOnly = 2
}
