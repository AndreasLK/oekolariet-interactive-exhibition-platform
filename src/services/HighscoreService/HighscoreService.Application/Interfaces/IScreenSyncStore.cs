using HighscoreService.Domain.Entities;

namespace HighscoreService.Application.Interfaces
{
        /// <summary>
        /// Screen-only persistence contract for outbox push, per-(source, game) watermarks, peer deltas, and 7-day pruning.
        /// </summary>
        public interface IScreenSyncStore
        {
                /// <summary>
                /// Reads pending outbox entries along with their queued sequence numbers.
                /// </summary>
                /// <param name="batchSize">Maximum batch size.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>Pending entities and their outbox sequence numbers.</returns>
                public Task<IReadOnlyList<(HighscoreEntity Entity, long OutboxSequence)>> GetPendingOutboxAsync(
                        int batchSize,
                        CancellationToken cancellationToken);

                /// <summary>
                /// Marks outbox entries as synced only if their queued sequence has not advanced during an in-flight push.
                /// </summary>
                /// <param name="ackedItems">Pairs of HighscoreId and pushed OutboxSequence.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                public Task MarkOutboxSyncedAsync(
                        IReadOnlyList<(Guid HighscoreId, long OutboxSequence)> ackedItems,
                        CancellationToken cancellationToken);

                /// <summary>
                /// Gets the stored pull sequence watermark for a specific source node and game (or <c>Guid.Empty</c> for tombstones).
                /// </summary>
                /// <param name="sourceNodeId">Source node identifier (e.g. "hub" or peer NodeId).</param>
                /// <param name="gameId">Game identifier or <see cref="Guid.Empty"/> for global tombstones.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>The stored sequence watermark (0 if none).</returns>
                public Task<long> GetPullWatermarkAsync(
                        string sourceNodeId,
                        Guid gameId,
                        CancellationToken cancellationToken);

                /// <summary>
                /// Saves the updated pull sequence watermark for a specific source node and game.
                /// </summary>
                /// <param name="sourceNodeId">Source node identifier.</param>
                /// <param name="gameId">Game identifier or <see cref="Guid.Empty"/> for tombstones.</param>
                /// <param name="sequence">The new watermark sequence.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                public Task SavePullWatermarkAsync(
                        string sourceNodeId,
                        Guid gameId,
                        long sequence,
                        CancellationToken cancellationToken);

                /// <summary>
                /// Applies incoming synced entries from Hub or a LAN peer idempotently.
                /// </summary>
                /// <param name="entries">Incoming entries.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>Count of newly inserted or updated rows.</returns>
                public Task<int> ApplySyncedBatchAsync(
                        IReadOnlyList<HighscoreEntity> entries,
                        CancellationToken cancellationToken);

                /// <summary>
                /// Applies incoming <see cref="GameDeletionTombstone"/> records locally.
                /// </summary>
                /// <param name="tombstones">Incoming tombstones.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                public Task ApplyGameDeletionTombstonesAsync(
                        IReadOnlyList<GameDeletionTombstone> tombstones,
                        CancellationToken cancellationToken);

                /// <summary>
                /// Returns local delta entries for a specific game so LAN peers can pull from this screen.
                /// </summary>
                /// <param name="gameId">Optional game identifier filter.</param>
                /// <param name="afterSequence">Exclusive local sequence watermark.</param>
                /// <param name="sinceUtc">Optional lower bound on <c>PlayedAtUtc</c>.</param>
                /// <param name="batchSize">Maximum batch size.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>Ordered delta entries.</returns>
                public Task<IReadOnlyList<HighscoreEntity>> GetLocalDeltaAsync(
                        Guid? gameId,
                        long afterSequence,
                        DateTimeOffset? sinceUtc,
                        int batchSize,
                        CancellationToken cancellationToken);

                /// <summary>
                /// Gets the highest local <c>SyncSequence</c> for UDP LAN discovery announcements.
                /// </summary>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>Maximum local sequence.</returns>
                public Task<long> GetMaxLocalSequenceAsync(CancellationToken cancellationToken);

                /// <summary>
                /// Prunes already-synced non-tombstone rows older than <paramref name="cutoffUtc"/> and runs incremental vacuum.
                /// </summary>
                /// <param name="cutoffUtc">Exclusive UTC cutoff timestamp.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>Number of pruned rows.</returns>
                public Task<int> PruneExpiredSyncedEntriesAsync(
                        DateTimeOffset cutoffUtc,
                        CancellationToken cancellationToken);
        }
}

