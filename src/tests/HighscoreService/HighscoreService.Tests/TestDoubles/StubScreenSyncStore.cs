using HighscoreService.Application.Interfaces;
using HighscoreService.Domain.Entities;

namespace HighscoreService.Tests.TestDoubles
{
        /// <summary>
        /// In-memory <see cref="IScreenSyncStore"/> test double capturing synced entities and pull watermarks.
        /// </summary>
        internal sealed class StubScreenSyncStore : IScreenSyncStore
        {
                /// <summary>
                /// Gets the entities applied via <see cref="ApplySyncedBatchAsync"/>.
                /// </summary>
                public List<HighscoreEntity> SyncedEntities { get; } = [];

                /// <summary>
                /// Gets the most recent pull watermark saved by the sync worker.
                /// </summary>
                public long LastWatermark { get; private set; }

                /// <inheritdoc />
                public Task<IReadOnlyList<(HighscoreEntity Entity, long OutboxSequence)>> GetPendingOutboxAsync(
                        int batchSize,
                        CancellationToken cancellationToken)
                {
                        // Return an empty outbox so tests focus on delta pull behavior.
                        return Task.FromResult<IReadOnlyList<(HighscoreEntity, long)>>([]);
                }

                /// <inheritdoc />
                public Task MarkOutboxSyncedAsync(
                        IReadOnlyList<(Guid HighscoreId, long OutboxSequence)> syncedItems,
                        CancellationToken cancellationToken)
                {
                        // No-op because this stub has no outbox queue.
                        return Task.CompletedTask;
                }

                /// <inheritdoc />
                public Task<long> GetPullWatermarkAsync(string sourceNodeId, Guid gameId, CancellationToken cancellationToken)
                {
                        // Start pulls from sequence 0 so all test deltas are fetched.
                        return Task.FromResult(0L);
                }

                /// <inheritdoc />
                public Task SavePullWatermarkAsync(string sourceNodeId, Guid gameId, long sequence, CancellationToken cancellationToken)
                {
                        // Record the watermark so tests can assert sync progress.
                        this.LastWatermark = sequence;
                        return Task.CompletedTask;
                }

                /// <inheritdoc />
                public Task<int> ApplySyncedBatchAsync(IReadOnlyList<HighscoreEntity> entries, CancellationToken cancellationToken)
                {
                        // Capture pulled entities for post-cycle assertions.
                        this.SyncedEntities.AddRange(entries);
                        return Task.FromResult(entries.Count);
                }

                /// <inheritdoc />
                public Task ApplyGameDeletionTombstonesAsync(
                        IReadOnlyList<GameDeletionTombstone> tombstones,
                        CancellationToken cancellationToken)
                {
                        // No-op in LAN fallback tests.
                        return Task.CompletedTask;
                }

                /// <inheritdoc />
                public Task<IReadOnlyList<HighscoreEntity>> GetLocalDeltaAsync(
                        Guid? gameId,
                        long afterSequence,
                        DateTimeOffset? sinceUtc,
                        int batchSize,
                        CancellationToken cancellationToken)
                {
                        // Return no local delta entries.
                        return Task.FromResult<IReadOnlyList<HighscoreEntity>>([]);
                }

                /// <inheritdoc />
                public Task<int> PruneExpiredSyncedEntriesAsync(DateTimeOffset olderThanUtc, CancellationToken cancellationToken)
                {
                        // Return 0 pruned entries in unit tests.
                        return Task.FromResult(0);
                }

                /// <inheritdoc />
                public Task<long> GetMaxLocalSequenceAsync(CancellationToken cancellationToken)
                {
                        // Return sequence 0 when no local scores exist.
                        return Task.FromResult(0L);
                }
        }
}

