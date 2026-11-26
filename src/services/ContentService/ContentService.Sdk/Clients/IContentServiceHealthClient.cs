using ContentService.Sdk.Models;

namespace ContentService.Sdk.Clients;

/// <summary>
/// Typed HTTP client contract for querying storage quota health telemetry from ContentService.
/// </summary>
public interface IContentServiceHealthClient
{
        /// <summary>
        /// Retrieves the current storage quota consumption and health status for the ContentService storage node.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the HTTP request.</param>
        /// <returns>The current <see cref="ContentStorageHealthReport"/>.</returns>
        public Task<ContentStorageHealthReport> GetStorageHealthAsync(CancellationToken cancellationToken = default);
}

