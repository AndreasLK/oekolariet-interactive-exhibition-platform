using HighscoreService.Application.Interfaces;
using HighscoreService.Domain.Entities;

namespace HighscoreService.Tests.TestDoubles
{
        /// <summary>
        /// In-memory <see cref="IHubSyncStore"/> test double tracking batch ingestion calls.
        /// </summary>
        internal sealed class StubHubSyncStore : IHubSyncStore
        {
                /// <summary>
                /// Gets the number of times <see cref="ApplySyncedBatchAsync"/> was invoked.
                /// </summary>
                public int ApplyBatchCallCount { get; private set; }

                /// <summary>
                /// Gets or sets the applied count returned by <see cref="ApplySyncedBatchAsync"/>.
                /// </summary>
                public int AppliedCountToReturn { get; set; }

                /// <inheritdoc />
                public Task<int> ApplySyncedBatchAsync(
                        IReadOnlyList<HighscoreEntity> entries,
                        CancellationToken cancellationToken)
                {
                        // Track invocation count so empty-batch short-circuits can be verified.
                        this.ApplyBatchCallCount++;
                        return Task.FromResult(this.AppliedCountToReturn);
                }

                /// <inheritdoc />
                public Task<IReadOnlyList<HighscoreEntity>> GetDeltaAsync(
                        Guid? gameId,
                        long afterSequence,
                        DateTimeOffset? sinceUtc,
                        int batchSize,
                        CancellationToken cancellationToken)
                {
                        // Return an empty delta list for unit tests.
                        return Task.FromResult<IReadOnlyList<HighscoreEntity>>([]);
                }

                /// <inheritdoc />
                public Task<IReadOnlyList<GameDeletionTombstone>> GetGameDeletionTombstonesAsync(
                        long afterSequence,
                        CancellationToken cancellationToken)
                {
                        // Return an empty tombstone list for unit tests.
                        return Task.FromResult<IReadOnlyList<GameDeletionTombstone>>([]);
                }
        }
}

