using HighscoreService.Facade.Dtos;

namespace HighscoreService.Application.Interfaces.Queries
{
        /// <summary>
        /// Queries incremental Hub delta entries and game-deletion tombstones for Screen sync.
        /// </summary>
        public interface IHubSyncQueries
        {
                /// <summary>
                /// Retrieves incremental delta entries after <paramref name="afterSequence"/>.
                /// </summary>
                /// <param name="gameId">Optional game filter.</param>
                /// <param name="afterSequence">Exclusive Hub sync sequence watermark.</param>
                /// <param name="sinceUtc">Optional lower timestamp bound for initial bootstrap.</param>
                /// <param name="batchSize">Maximum batch size.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>Ordered delta DTOs.</returns>
                public Task<IReadOnlyList<HighscoreDto>> GetDeltaAsync(
                        Guid? gameId,
                        long afterSequence,
                        DateTimeOffset? sinceUtc,
                        int batchSize,
                        CancellationToken cancellationToken);

                /// <summary>
                /// Retrieves game-deletion tombstones recorded after <paramref name="afterSequence"/>.
                /// </summary>
                /// <param name="afterSequence">Exclusive tombstone sync sequence watermark.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>Ordered game deletion tombstone DTOs.</returns>
                public Task<IReadOnlyList<GameDeletionTombstoneDto>> GetGameDeletionTombstonesAsync(
                        long afterSequence,
                        CancellationToken cancellationToken);
        }
}

