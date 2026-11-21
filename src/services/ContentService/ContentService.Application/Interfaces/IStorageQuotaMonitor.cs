namespace ContentService.Application.Interfaces;

/// <summary>
/// Queries the total number of bytes currently consumed by stored image binaries on the local storage node.
/// </summary>
public interface IStorageQuotaMonitor
{
        /// <summary>
        /// Returns the current cumulative byte usage across stored image binaries.
        /// </summary>
        /// <param name="cancellationToken">A token to observe while computing storage usage.</param>
        /// <returns>The total consumed byte count on the storage node.</returns>
        public Task<long> GetUsedBytesAsync(CancellationToken cancellationToken);
}

