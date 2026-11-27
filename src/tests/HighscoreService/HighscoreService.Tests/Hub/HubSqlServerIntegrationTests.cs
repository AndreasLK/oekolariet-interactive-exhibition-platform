using HighscoreService.Application.Models;
using HighscoreService.Application.UseCases;
using HighscoreService.Domain.Entities;
using HighscoreService.Domain.Specifications;
using HighscoreService.Hub.Infrastructure.Persistence;
using HighscoreService.Hub.Infrastructure.Repositories;

namespace HighscoreService.Tests.Hub
{
        /// <summary>
        /// SQL Server Testcontainers integration tests verifying Hub stores, transactions,
        /// monotonic sequence allocation, rolling ranks, tombstones, and sync ingestion.
        /// </summary>
        [Collection(MsSqlHighscoreDatabaseCollection.COLLECTION_NAME)]
        public sealed class HubSqlServerIntegrationTests(MsSqlHighscoreDatabaseFixture fixture)
        {
                private const string MAC_A = "AA:BB:CC:DD:EE:01";
                private const string MAC_B = "AA:BB:CC:DD:EE:02";
                private readonly MsSqlHighscoreDatabaseFixture _fixture = fixture;

                [Fact]
                public async Task HubHighscoreStore_EnforcesIdempotencyAndRollingRank()
                {
                        await this._fixture.ResetDatabaseAsync();
                        DateTimeOffset baseTime = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
                        Guid gameId = Guid.CreateVersion7();

                        await using HubHighscoreDbContext dbContext = this._fixture.CreateDbContext();
                        HubHighscoreStore store = new(dbContext);

                        HighscoreEntity first = HighscoreEntity.Create(
                                id: Guid.CreateVersion7(),
                                name: "Alice",
                                age: 12,
                                score: 500,
                                senderMacAddress: MAC_A,
                                gameId: gameId,
                                playedAtUtc: baseTime);

                        HighscoreInsertOutcome firstOutcome = await store.AddIdempotentAsync(first, 24, TestContext.Current.CancellationToken);
                        HighscoreInsertOutcome duplicateOutcome = await store.AddIdempotentAsync(first, 24, TestContext.Current.CancellationToken);

                        Assert.True(firstOutcome.WasInserted);
                        Assert.False(duplicateOutcome.WasInserted);
                        Assert.Equal(1, firstOutcome.Entity.RankingAtTimeOfScoring);
                        Assert.True(firstOutcome.Entity.SyncSequence > 0);

                        HighscoreEntity second = HighscoreEntity.Create(
                                id: Guid.CreateVersion7(),
                                name: "Bob",
                                age: 14,
                                score: 300,
                                senderMacAddress: MAC_B,
                                gameId: gameId,
                                playedAtUtc: baseTime.AddMinutes(5));

                        HighscoreInsertOutcome secondOutcome = await store.AddIdempotentAsync(second, 24, TestContext.Current.CancellationToken);
                        Assert.True(secondOutcome.WasInserted);
                        Assert.Equal(2, secondOutcome.Entity.RankingAtTimeOfScoring);
                }

                [Fact]
                public async Task HubHighscoreStore_SoftDeleteByGameId_CreatesTombstoneAndCascadesToLateArrivals()
                {
                        await this._fixture.ResetDatabaseAsync();
                        DateTimeOffset baseTime = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
                        Guid gameId = Guid.CreateVersion7();

                        await using HubHighscoreDbContext dbContext = this._fixture.CreateDbContext();
                        HubHighscoreStore store = new(dbContext);

                        HighscoreEntity active = HighscoreEntity.Create(
                                id: Guid.CreateVersion7(),
                                name: "Alice",
                                age: 10,
                                score: 250,
                                senderMacAddress: MAC_A,
                                gameId: gameId,
                                playedAtUtc: baseTime);

                        await store.AddIdempotentAsync(active, 24, TestContext.Current.CancellationToken);

                        DateTimeOffset deleteTime = baseTime.AddMinutes(10);
                        int deletedCount = await store.SoftDeleteByGameIdAsync(gameId, deleteTime, TestContext.Current.CancellationToken);
                        Assert.Equal(1, deletedCount);

                        // Late arrival played BEFORE the game deletion must automatically be marked deleted.
                        HighscoreEntity lateArrivalBeforeDelete = HighscoreEntity.Create(
                                id: Guid.CreateVersion7(),
                                name: "OfflinePlayer",
                                age: 11,
                                score: 999,
                                senderMacAddress: MAC_B,
                                gameId: gameId,
                                playedAtUtc: baseTime.AddMinutes(5));

                        HighscoreInsertOutcome lateOutcome = await store.AddIdempotentAsync(lateArrivalBeforeDelete, 24, TestContext.Current.CancellationToken);
                        Assert.True(lateOutcome.WasInserted);
                        Assert.True(lateOutcome.Entity.IsDeleted);
                        Assert.Null(lateOutcome.Entity.RankingAtTimeOfScoring);

                        // Entry played AFTER the game deletion remains active.
                        HighscoreEntity freshPlayAfterDelete = HighscoreEntity.Create(
                                id: Guid.CreateVersion7(),
                                name: "FreshPlayer",
                                age: 11,
                                score: 400,
                                senderMacAddress: MAC_B,
                                gameId: gameId,
                                playedAtUtc: deleteTime.AddMinutes(1));

                        HighscoreInsertOutcome freshOutcome = await store.AddIdempotentAsync(freshPlayAfterDelete, 24, TestContext.Current.CancellationToken);
                        Assert.False(freshOutcome.Entity.IsDeleted);
                        Assert.Equal(1, freshOutcome.Entity.RankingAtTimeOfScoring);
                }

                [Fact]
                public async Task HubSyncIngestStore_AppliesBatchIdempotentlyAndServesOrderedDeltasAndTombstones()
                {
                        await this._fixture.ResetDatabaseAsync();
                        DateTimeOffset baseTime = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
                        Guid gameA = Guid.CreateVersion7();
                        Guid gameB = Guid.CreateVersion7();

                        await using HubHighscoreDbContext dbContext = this._fixture.CreateDbContext();
                        HubHighscoreStore highscoreStore = new(dbContext);
                        HubSyncIngestStore syncStore = new(dbContext);

                        // Create a tombstone for gameB before syncing an offline entry for gameB.
                        await highscoreStore.SoftDeleteByGameIdAsync(gameB, baseTime.AddMinutes(10), TestContext.Current.CancellationToken);

                        HighscoreEntity entryA = HighscoreEntity.Create(
                                id: Guid.CreateVersion7(),
                                name: "Anna",
                                age: 15,
                                score: 700,
                                senderMacAddress: MAC_A,
                                gameId: gameA,
                                playedAtUtc: baseTime);

                        HighscoreEntity entryBStale = HighscoreEntity.Create(
                                id: Guid.CreateVersion7(),
                                name: "Stale",
                                age: 16,
                                score: 800,
                                senderMacAddress: MAC_B,
                                gameId: gameB,
                                playedAtUtc: baseTime.AddMinutes(2));

                        int firstApplied = await syncStore.ApplySyncedBatchAsync([entryA, entryBStale], TestContext.Current.CancellationToken);
                        int secondApplied = await syncStore.ApplySyncedBatchAsync([entryA, entryBStale], TestContext.Current.CancellationToken);

                        Assert.Equal(2, firstApplied);
                        Assert.Equal(0, secondApplied);

                        IReadOnlyList<HighscoreEntity> deltaGameA = await syncStore.GetDeltaAsync(gameA, 0, null, 50, TestContext.Current.CancellationToken);
                        IReadOnlyList<GameDeletionTombstone> tombstones = await syncStore.GetGameDeletionTombstonesAsync(0, TestContext.Current.CancellationToken);

                        var item = Assert.Single(deltaGameA);
                        Assert.Equal("Anna", item.Name);
                        var item_2 = Assert.Single(tombstones);
                        Assert.Equal(gameB, item_2.GameId);

                        IReadOnlyList<HighscoreEntity> deltaGameB = await syncStore.GetDeltaAsync(gameB, 0, null, 50, TestContext.Current.CancellationToken);
                        var item_3 = Assert.Single(deltaGameB);
                        Assert.True(item_3.IsDeleted);
                }

                [Fact]
                public async Task HubStatisticsStore_QueriesSqlServerHistoryAndBuildsAccurateReport()
                {
                        await this._fixture.ResetDatabaseAsync();
                        DateTimeOffset baseTime = new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);
                        Guid gameId = Guid.CreateVersion7();

                        await using HubHighscoreDbContext dbContext = this._fixture.CreateDbContext();
                        HubHighscoreStore highscoreStore = new(dbContext);
                        HubStatisticsStore statsStore = new(dbContext);

                        await highscoreStore.AddIdempotentAsync(
                                HighscoreEntity.Create(Guid.CreateVersion7(), "P1", 9, 100, MAC_A, gameId, baseTime),
                                24,
                                TestContext.Current.CancellationToken);
                        await highscoreStore.AddIdempotentAsync(
                                HighscoreEntity.Create(Guid.CreateVersion7(), null, null, 40, MAC_A, gameId, baseTime.AddMinutes(5), isTimedOut: true),
                                24,
                                TestContext.Current.CancellationToken);

                        HighscoreSpecification spec = new()
                        {
                                GameId = gameId,
                                IncludeUnnamed = true,
                                IncludeTimedOut = true
                        };

                        IReadOnlyList<HighscoreEntity> entries = await statsStore.GetAllForStatisticsAsync(
                                spec,
                                baseTime.AddHours(1),
                                TestContext.Current.CancellationToken);

                        HighscoreStatisticsReport report = HighscoreStatisticsCalculator.BuildReport(entries, baseTime.AddHours(1));

                        Assert.Equal(2, report.TotalPlays);
                        Assert.Equal(1, report.CompletedPlays);
                        Assert.Equal(1, report.TimedOutPlays);
                        Assert.Equal(50.0, report.AbandonmentRatePercent);
                }

                /// <summary>
                /// Verifies <see cref="HubScreenRegistrationStore.EnrollIdempotentAsync"/> enforces the pending queue cap
                /// while still allowing idempotent retries for already-enrolled screens.
                /// </summary>
                [Fact]
                public async Task HubScreenRegistrationStore_EnforcesPendingQueueCap()
                {
                        // Arrange: Seed 2 pending screens with maxPendingCount = 2.
                        await this._fixture.ResetDatabaseAsync();
                        await using HubHighscoreDbContext dbContext = this._fixture.CreateDbContext();
                        HubScreenRegistrationStore store = new(dbContext);
                        CancellationToken ct = TestContext.Current.CancellationToken;

                        ScreenRegistration first = ScreenRegistration.Enroll(
                                Guid.CreateVersion7(), "kiosk-1", null, "secret-1-at-least-16-chars", DateTimeOffset.UtcNow);
                        ScreenRegistration second = ScreenRegistration.Enroll(
                                Guid.CreateVersion7(), "kiosk-2", null, "secret-2-at-least-16-chars", DateTimeOffset.UtcNow);
                        ScreenRegistration third = ScreenRegistration.Enroll(
                                Guid.CreateVersion7(), "kiosk-3", null, "secret-3-at-least-16-chars", DateTimeOffset.UtcNow);

                        // Act
                        HighscoreService.Application.Models.ScreenEnrollmentOutcome firstOutcome = await store.EnrollIdempotentAsync(first, maxPendingCount: 2, ct);
                        HighscoreService.Application.Models.ScreenEnrollmentOutcome secondOutcome = await store.EnrollIdempotentAsync(second, maxPendingCount: 2, ct);
                        HighscoreService.Application.Models.ScreenEnrollmentOutcome cappedOutcome = await store.EnrollIdempotentAsync(third, maxPendingCount: 2, ct);
                        HighscoreService.Application.Models.ScreenEnrollmentOutcome retryOutcome = await store.EnrollIdempotentAsync(first, maxPendingCount: 2, ct);

                        // Assert: New 3rd enrollment is rejected, while idempotent retry of 1st succeeds.
                        Assert.True(firstOutcome.WasCreated);
                        Assert.True(secondOutcome.WasCreated);
                        Assert.True(cappedOutcome.PendingCapReached);
                        Assert.Null(cappedOutcome.Registration);
                        Assert.False(retryOutcome.WasCreated);
                        Assert.False(retryOutcome.PendingCapReached);
                        Assert.NotNull(retryOutcome.Registration);
                }
        }
}
