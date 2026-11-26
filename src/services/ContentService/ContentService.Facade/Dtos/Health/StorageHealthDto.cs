namespace ContentService.Facade.Dtos;

/// <summary>
/// Facade DTO reporting current disk quota consumption and health status for a ContentService storage node.
/// </summary>
public sealed record class StorageHealthDto
{
        /// <summary>
        /// Gets the evaluated storage health status.
        /// </summary>
        public required StorageHealthStatusDto Status { get; init; }

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

