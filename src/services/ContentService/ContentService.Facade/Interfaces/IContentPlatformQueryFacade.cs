using ContentService.Facade.Dtos;

namespace ContentService.Facade.Interfaces;

/// <summary>
/// CQS query facade contract for read-only publishing bundle resolution and storage quota telemetry.
/// </summary>
public interface IContentPlatformQueryFacade
{
        /// <summary>
        /// Resolves all image references and optional template schema for an exhibition implementation into a checksum-sealed bundle.
        /// </summary>
        /// <param name="query">The publish bundle resolution query DTO.</param>
        /// <param name="cancellationToken">Propagates caller cancellation during asset verification.</param>
        /// <returns>The resolved <see cref="PublishBundleDto"/>.</returns>
        public Task<PublishBundleDto> ResolvePublishBundleAsync(
                ResolvePublishBundleQueryDto query,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Evaluates current disk quota consumption and health status for the active ContentService storage node.
        /// </summary>
        /// <param name="cancellationToken">Propagates caller cancellation during quota aggregation.</param>
        /// <returns>The current <see cref="StorageHealthDto"/>.</returns>
        public Task<StorageHealthDto> GetStorageHealthAsync(CancellationToken cancellationToken = default);
}

