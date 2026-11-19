namespace ContentService.Domain.Enums;

public enum StorageHealthStatus
{
        Healthy = 0,
        Warning = 1,
        // Transitions a full shard to read-only instead of crashing writes unpredictably, keeping existing exhibition kiosks serving previously uploaded assets while IT adds a new node.
        FullReadOnly = 2
}

