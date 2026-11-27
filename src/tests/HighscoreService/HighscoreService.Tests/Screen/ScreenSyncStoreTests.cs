using HighscoreService.Domain.Entities;
using HighscoreService.Screen.Infrastructure.Persistence;
using HighscoreService.Screen.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HighscoreService.Tests.Screen
{
        /// <summary>
        /// SQLite integration tests for <see cref="ScreenSyncStore"/> outbox safety, tombstones, watermarks, and pruning.
        /// </summary>
        public sealed class ScreenSyncStoreTests : IAsyncLifetime
        {
                private readonly string _dbPath;
                private readonly HighscoreWriteGate _writeGate = new();
                private readonly ScreenHighscoreDbContext _dbContext;
                private readonly ScreenHighscoreStore _highscoreStore;
                private readonly ScreenSyncStore _syncStore;

                /// <summary>
                /// Initializes a new instance of the <see cref="ScreenSyncStoreTests"/> class.
                /// </summary>
                public ScreenSyncStoreTests()
                {
                        this._dbPath = Path.Combine(Path.GetTempPath(), $"highscore_sync_store_test_{Guid.NewGuid():N}.db");
                        DbContextOptions<ScreenHighscoreDbContext> options = new DbContextOptionsBuilder<ScreenHighscoreDbContext>()
                                .UseSqlite($"Data Source={this._dbPath};Pooling=False")
                                .AddInterceptors(new SqlitePragmaConnectionInterceptor())
                                .Options;

                        this._dbContext = new ScreenHighscoreDbContext(options);
                        this._highscoreStore = new ScreenHighscoreStore(this._dbContext, this._writeGate);
                        this._syncStore = new ScreenSyncStore(this._dbContext, this._writeGate);
                }

                /// <inheritdoc />
                public async ValueTask InitializeAsync()
                {
                        // Initialize SQLite schema and incremental auto-vacuum for each test instance.
                        await this._dbContext.EnsureIncrementalAutoVacuumAsync(TestContext.Current.CancellationToken);
                        await this._dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
                }

                /// <inheritdoc />
                public async ValueTask DisposeAsync()
                {
                        // Clean up database context, semaphore gate, and temporary SQLite file.
                        await this._dbContext.DisposeAsync();
                        this._writeGate.Dispose();
                        if (File.Exists(this._dbPath))
                        {
                                File.Delete(this._dbPath);
                        }
                }

                /// <summary>
                /// Verifies <see cref="ScreenSyncStore.MarkOutboxSyncedAsync"/> preserves outbox entries when a newer local mutation
                /// (such as soft deletion) occurred while the previous HTTP push was in flight.
                /// </summary>
                [Fact]
                public async Task MarkOutboxSyncedAsync_PreservesEntry_WhenNewerLocalMutationOccurred()
                {
                        // Arrange: Insert a score (QueuedSequence = 1), read pending outbox, then soft-delete locally (QueuedSequence = 2).
                        CancellationToken ct = TestContext.Current.CancellationToken;
                        HighscoreEntity entity = HighscoreEntity.Create(
                                id: Guid.CreateVersion7(),
                                name: "RacePlayer",
                                age: 12,
                                score: 400,
                                senderMacAddress: "AA:BB:CC:DD:EE:01",
                                gameId: Guid.CreateVersion7(),
                                playedAtUtc: DateTimeOffset.UtcNow);

                        await this._highscoreStore.AddIdempotentAsync(entity, 5, ct);
                        IReadOnlyList<(HighscoreEntity Entity, long OutboxSequence)> inFlight =
                                await this._syncStore.GetPendingOutboxAsync(10, ct);
                        (HighscoreEntity pushedEntity, long pushedSeq) = Assert.Single(inFlight);

                        // Mutate locally before acknowledging the earlier push.
                        await this._highscoreStore.SoftDeleteByIdAsync(entity.Id, DateTimeOffset.UtcNow, ct);

                        // Act: Acknowledge the stale sequence 1.
                        await this._syncStore.MarkOutboxSyncedAsync([(pushedEntity.Id, pushedSeq)], ct);

                        // Assert: Outbox still contains sequence 2 so the soft-delete will be pushed on next cycle.
                        IReadOnlyList<(HighscoreEntity Entity, long OutboxSequence)> remaining =
                                await this._syncStore.GetPendingOutboxAsync(10, ct);
                        (HighscoreEntity remainingEntity, long remainingSeq) = Assert.Single(remaining);
                        Assert.True(remainingEntity.IsDeleted);
                        Assert.True(remainingSeq > pushedSeq);
                }

                /// <summary>
                /// Verifies tombstones soft-delete existing local entries and auto-delete late-arriving peer scores for the deleted game.
                /// </summary>
                [Fact]
                public async Task ApplyGameDeletionTombstonesAsync_SoftDeletesExistingAndBlocksLateArrivals()
                {
                        // Arrange
                        CancellationToken ct = TestContext.Current.CancellationToken;
                        Guid deletedGameId = Guid.CreateVersion7();
                        DateTimeOffset playTime = DateTimeOffset.UtcNow.AddMinutes(-10);
                        DateTimeOffset deleteTime = DateTimeOffset.UtcNow;

                        HighscoreEntity existing = HighscoreEntity.Create(
                                id: Guid.CreateVersion7(),
                                name: "Existing",
                                age: 11,
                                score: 200,
                                senderMacAddress: "AA:BB:CC:DD:EE:01",
                                gameId: deletedGameId,
                                playedAtUtc: playTime);

                        await this._syncStore.ApplySyncedBatchAsync([existing], ct);

                        // Act: Apply tombstone and then a late-arriving peer entry played before deletion.
                        GameDeletionTombstone tombstone = GameDeletionTombstone.Create(deletedGameId, deleteTime, syncSequence: 1);
                        await this._syncStore.ApplyGameDeletionTombstonesAsync([tombstone], ct);

                        HighscoreEntity lateArrival = HighscoreEntity.Create(
                                id: Guid.CreateVersion7(),
                                name: "LatePeer",
                                age: 11,
                                score: 999,
                                senderMacAddress: "AA:BB:CC:DD:EE:02",
                                gameId: deletedGameId,
                                playedAtUtc: playTime.AddMinutes(1));

                        await this._syncStore.ApplySyncedBatchAsync([lateArrival], ct);

                        // Assert: Both entries are soft-deleted locally.
                        List<HighscoreEntity> stored = await this._dbContext.Highscores.AsNoTracking().ToListAsync(ct);
                        Assert.Equal(2, stored.Count);
                        Assert.All(stored, item => Assert.True(item.IsDeleted));
                }

                /// <summary>
                /// Verifies <see cref="ScreenSyncStore.PruneExpiredSyncedEntriesAsync"/> never deletes unsynced outbox items or soft-deleted rows.
                /// </summary>
                [Fact]
                public async Task PruneExpiredSyncedEntriesAsync_NeverDeletesUnsyncedOutboxOrSoftDeletedEntries()
                {
                        // Arrange: One old unsynced outbox item and one old synced soft-deleted item.
                        CancellationToken ct = TestContext.Current.CancellationToken;
                        DateTimeOffset oldTime = DateTimeOffset.UtcNow.AddDays(-14);
                        Guid gameId = Guid.CreateVersion7();

                        HighscoreEntity unsyncedOld = HighscoreEntity.Create(
                                id: Guid.CreateVersion7(),
                                name: "Unsynced",
                                age: 10,
                                score: 100,
                                senderMacAddress: "AA:BB:CC:DD:EE:01",
                                gameId: gameId,
                                playedAtUtc: oldTime);
                        await this._highscoreStore.AddIdempotentAsync(unsyncedOld, 5, ct);

                        HighscoreEntity deletedOld = HighscoreEntity.Create(
                                id: Guid.CreateVersion7(),
                                name: "Deleted",
                                age: 10,
                                score: 150,
                                senderMacAddress: "AA:BB:CC:DD:EE:02",
                                gameId: gameId,
                                playedAtUtc: oldTime);
                        deletedOld.MarkDeleted(oldTime.AddMinutes(1));
                        await this._syncStore.ApplySyncedBatchAsync([deletedOld], ct);

                        // Act
                        int pruned = await this._syncStore.PruneExpiredSyncedEntriesAsync(DateTimeOffset.UtcNow.AddDays(-7), ct);

                        // Assert
                        Assert.Equal(0, pruned);
                        Assert.Equal(2, await this._dbContext.Highscores.CountAsync(ct));
                }

                /// <summary>
                /// Verifies <see cref="ScreenSyncStore.SavePullWatermarkAsync"/> ignores stale sequence regressions.
                /// </summary>
                [Fact]
                public async Task SavePullWatermarkAsync_EnforcesMonotonicProgress()
                {
                        // Arrange & Act
                        CancellationToken ct = TestContext.Current.CancellationToken;
                        Guid gameId = Guid.Empty;
                        await this._syncStore.SavePullWatermarkAsync("hub", gameId, 25, ct);
                        await this._syncStore.SavePullWatermarkAsync("hub", gameId, 10, ct);

                        // Assert
                        long current = await this._syncStore.GetPullWatermarkAsync("hub", gameId, ct);
                        Assert.Equal(25, current);
                }
        }
}

