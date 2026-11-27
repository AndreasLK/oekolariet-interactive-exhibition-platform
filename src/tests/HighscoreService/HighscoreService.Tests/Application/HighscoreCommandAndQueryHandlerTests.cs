using HighscoreService.Application.CommandHandlers;
using HighscoreService.Application.Options;
using HighscoreService.Application.QueryHandlers;
using HighscoreService.Domain.Exceptions;
using HighscoreService.Facade.Commands;
using HighscoreService.Facade.Dtos;
using HighscoreService.Tests.TestDoubles;
using Microsoft.Extensions.Options;

namespace HighscoreService.Tests.Application
{
        /// <summary>
        /// Unit tests for Application command and query handler guard clauses, defaults, and mapping.
        /// </summary>
        public sealed class HighscoreCommandAndQueryHandlerTests
        {
                /// <summary>
                /// Verifies <see cref="QueryHighscoresHandler"/> applies <see cref="RankingOptions.DefaultLeaderboardWindowHours"/>
                /// when no explicit time range is provided, and preserves explicit windows when supplied.
                /// </summary>
                [Fact]
                public async Task QueryHighscoresHandler_AppliesDefaultWindowOnlyWhenTimeRangeOmitted()
                {
                        // Arrange
                        StubHighscoreStore store = new();
                        FixedTimeProvider clock = new(new DateTimeOffset(2026, 11, 27, 12, 0, 0, TimeSpan.Zero));
                        QueryHighscoresHandler handler = new(
                                store: store,
                                options: Options.Create(new RankingOptions { DefaultLeaderboardWindowHours = 24 }),
                                timeProvider: clock);

                        // Act 1: No time range -> defaults to LastHours = 24.
                        await handler.HandleAsync(new HighscoreQueryDto(), TestContext.Current.CancellationToken);
                        Assert.Equal(24, store.LastQuerySpecification?.LastHours);

                        // Act 2: Explicit FromUtc -> LastHours remains null.
                        DateTimeOffset explicitFrom = clock.GetUtcNow().AddDays(-3);
                        await handler.HandleAsync(
                                new HighscoreQueryDto(FromUtc: explicitFrom),
                                TestContext.Current.CancellationToken);

                        Assert.Equal(explicitFrom, store.LastQuerySpecification?.FromUtc);
                        Assert.Null(store.LastQuerySpecification?.LastHours);
                }

                /// <summary>
                /// Verifies <see cref="ApplySyncBatchCommandHandler"/> short-circuits empty batches without hitting the store.
                /// </summary>
                [Fact]
                public async Task ApplySyncBatchCommandHandler_ShortCircuitsEmptyBatch_AndMapsNonEmptyBatch()
                {
                        // Arrange
                        StubHubSyncStore syncStore = new() { AppliedCountToReturn = 1 };
                        ApplySyncBatchCommandHandler handler = new(syncStore);

                        // Act 1: Empty batch should not invoke persistence.
                        SyncBatchResultDto emptyResult = await handler.HandleAsync(
                                new ApplySyncBatchCommand([]),
                                TestContext.Current.CancellationToken);

                        Assert.Equal(0, emptyResult.ReceivedCount);
                        Assert.Equal(0, emptyResult.AppliedCount);
                        Assert.Equal(0, syncStore.ApplyBatchCallCount);

                        // Act 2: Non-empty batch maps to domain entities and delegates to store.
                        HighscoreDto dto = new(
                                Id: Guid.CreateVersion7(),
                                Name: "Ada",
                                Age: 14,
                                Score: 900,
                                SenderMacAddress: "aa-bb-cc-dd-ee-01",
                                GameId: Guid.CreateVersion7(),
                                PlayedAtUtc: DateTimeOffset.UtcNow,
                                RankingAtTimeOfScoring: 1,
                                IsTimedOut: false,
                                IsDeleted: false,
                                DeletedAtUtc: null,
                                SyncSequence: 1);

                        SyncBatchResultDto nonEmptyResult = await handler.HandleAsync(
                                new ApplySyncBatchCommand([dto]),
                                TestContext.Current.CancellationToken);

                        Assert.Equal(1, nonEmptyResult.ReceivedCount);
                        Assert.Equal(1, nonEmptyResult.AppliedCount);
                        Assert.Equal(1, syncStore.ApplyBatchCallCount);
                }

                /// <summary>
                /// Verifies delete command handlers reject empty GUIDs and surface <see cref="EntityNotFoundException"/> for missing entries.
                /// </summary>
                [Fact]
                public async Task DeleteCommandHandlers_ValidateIdsAndSurfaceNotFound()
                {
                        // Arrange
                        StubHighscoreStore store = new() { SoftDeleteByIdResult = false };
                        FixedTimeProvider clock = new(DateTimeOffset.UtcNow);
                        DeleteHighscoreCommandHandler deleteById = new(store, clock);
                        DeleteGameHighscoresCommandHandler deleteByGame = new(store, clock);

                        // Act & Assert
                        await Assert.ThrowsAsync<DomainValidationException>(() =>
                                deleteById.HandleAsync(new DeleteHighscoreCommand(Guid.Empty), TestContext.Current.CancellationToken));

                        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
                                deleteById.HandleAsync(new DeleteHighscoreCommand(Guid.CreateVersion7()), TestContext.Current.CancellationToken));

                        await Assert.ThrowsAsync<DomainValidationException>(() =>
                                deleteByGame.HandleAsync(new DeleteGameHighscoresCommand(Guid.Empty), TestContext.Current.CancellationToken));
                }

                /// <summary>
                /// Verifies <see cref="RecordHighscoreCommandHandler"/> generates ID and timestamp when omitted by the caller.
                /// </summary>
                [Fact]
                public async Task RecordHighscoreCommandHandler_GeneratesIdAndTimestamp_WhenOmitted()
                {
                        // Arrange
                        StubHighscoreStore store = new();
                        DateTimeOffset fixedNow = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);
                        FixedTimeProvider clock = new(fixedNow);
                        RecordHighscoreCommandHandler handler = new(
                                store: store,
                                options: Options.Create(new RankingOptions { RollingRankingWindowHours = 5 }),
                                timeProvider: clock);

                        // Act
                        HighscoreDto result = await handler.HandleAsync(
                                new RecordHighscoreCommand(
                                        Id: null,
                                        Name: "Ada",
                                        Age: 12,
                                        Score: 320,
                                        SenderMacAddress: "aa-bb-cc-dd-ee-01",
                                        GameId: Guid.CreateVersion7(),
                                        PlayedAtUtc: null,
                                        IsTimedOut: false),
                                TestContext.Current.CancellationToken);

                        // Assert
                        Assert.NotEqual(Guid.Empty, result.Id);
                        Assert.Equal(fixedNow, result.PlayedAtUtc);
                        Assert.Equal("AA:BB:CC:DD:EE:01", result.SenderMacAddress);
                }
        }
}

