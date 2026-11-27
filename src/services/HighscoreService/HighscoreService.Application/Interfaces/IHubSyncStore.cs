using HighscoreService.Domain.Entities;

namespace HighscoreService.Application.Interfaces
{
        /// <summary>
        /// Hub-only persistence contract for receiving screen batches and serving per-game delta streams.
        /// </summary>
        public interface IHubSyncStore
        {
                /// <summary>
                /// Idempotently applies incoming synced entries while enforcing <see cref="GameDeletionTombstone"/> rules.
                /// </summary>
                /// <param name="entries">Incoming highscore entities.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>Count of newly inserted or updated rows.</returns>
                public Task<int> ApplySyncedBatchAsync(
                        IReadOnlyList<HighscoreEntity> entries,
                        CancellationToken cancellationToken);

                /// <summary>
                /// Returns delta entries for a specific game after <paramref name="afterSequence"/>, optionally bounded by <paramref name="sinceUtc"/>.
                /// </summary>
                /// <param name="gameId">Optional game identifier filter.</param>
                /// <param name="afterSequence">Exclusive monotonic sequence watermark.</param>
                /// <param name="sinceUtc">Optional lower bound on <c>PlayedAtUtc</c> for initial 7-day hydration.</param>
                /// <param name="batchSize">Maximum batch size.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>Ordered delta entries.</returns>
                public Task<IReadOnlyList<HighscoreEntity>> GetDeltaAsync(
                        Guid? gameId,
                        long afterSequence,
                        DateTimeOffset? sinceUtc,
                        int batchSize,
                        CancellationToken cancellationToken);

                /// <summary>
                /// Returns all active <see cref="GameDeletionTombstone"/> records after <paramref name="afterSequence"/>.
                /// </summary>
                /// <param name="afterSequence">Exclusive sequence watermark.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>Ordered game deletion tombstones.</returns>
                public Task<IReadOnlyList<GameDeletionTombstone>> GetGameDeletionTombstonesAsync(
                        long afterSequence,
                        CancellationToken cancellationToken);
        }
}

