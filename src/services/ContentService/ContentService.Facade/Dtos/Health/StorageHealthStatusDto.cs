namespace ContentService.Facade.Dtos;

/// <summary>
/// Represents the disk capacity health state of a ContentService storage node at the facade boundary.
/// </summary>
public enum StorageHealthStatusDto
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

