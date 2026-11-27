using HighscoreService.Application.Interfaces;
using HighscoreService.Domain.Entities;
using HighscoreService.Screen.Api;
using HighscoreService.Sdk.Client;
using HighscoreService.Sdk.Models.Highscores.Requests;
using HighscoreService.Sdk.Models.Highscores.Responses;
using HighscoreService.Tests.TestDoubles;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HighscoreService.Tests.Api
{
        /// <summary>
        /// End-to-end integration tests for Screen API, SQLite non-WAL concurrency, 7-day retention pruning, and SDK failover.
        /// </summary>
        public sealed class HighscoreApiIntegrationTests : IAsyncLifetime
        {
                private readonly string _dbPath;
                private readonly WebApplicationFactory<ScreenApiMarker> _factory;
                private readonly HighscoreServiceClient _client;

                /// <summary>
                /// Initializes a new instance of the <see cref="HighscoreApiIntegrationTests"/> class.
                /// </summary>
                public HighscoreApiIntegrationTests()
                {
                        this._dbPath = Path.Combine(Path.GetTempPath(), $"highscore_screen_test_{Guid.NewGuid():N}.db");
                        this._factory = new WebApplicationFactory<ScreenApiMarker>()
                                .WithWebHostBuilder(builder =>
                                {
                                        builder.ConfigureAppConfiguration((_, config) =>
                                        {
                                                config.AddInMemoryCollection(new Dictionary<string, string?>
                                                {
                                                        ["ConnectionStrings:HighscoreDatabase"] = $"Data Source={this._dbPath}",
                                                        ["HighscoreRanking:RollingRankingWindowHours"] = "5",
                                                        ["ScreenSync:EnableUdpDiscovery"] = "false",
                                                        ["ScreenSync:RetentionDays"] = "7"
                                                });
                                        });
                                });

                        HttpClient httpClient = this._factory.CreateClient();
                        this._client = new HighscoreServiceClient(httpClient);
                }

                /// <inheritdoc />
                public ValueTask InitializeAsync() => ValueTask.CompletedTask;

                /// <inheritdoc />
                public async ValueTask DisposeAsync()
                {
                        await this._factory.DisposeAsync();
                        if (File.Exists(this._dbPath))
                        {
                                File.Delete(this._dbPath);
                        }
                }

                /// <summary>
                /// Verifies recording, tie-breaking query sort order, and bulk deletion by game against Screen SQLite.
                /// </summary>
                [Fact]
                public async Task ScreenApi_RecordsQueriesSortsAndDeletesByGameId()
                {
                        // Arrange two tied scores played 15 minutes apart.
                        CancellationToken ct = TestContext.Current.CancellationToken;
                        Guid gameId = Guid.NewGuid();
                        DateTimeOffset now = DateTimeOffset.UtcNow;

                        HighscoreResponse first = await this._client.RecordAsync(
                                request: new RecordHighscoreRequest(
                                        Id: null,
                                        Name: "OlderTie",
                                        Age: 11,
                                        Score: 500,
                                        SenderMacAddress: "AA:BB:CC:DD:EE:01",
                                        GameId: gameId,
                                        PlayedAtUtc: now.AddMinutes(-20)),
                                cancellationToken: ct);

                        HighscoreResponse second = await this._client.RecordAsync(
                                request: new RecordHighscoreRequest(
                                        Id: null,
                                        Name: "NewerTie",
                                        Age: 14,
                                        Score: 500,
                                        SenderMacAddress: "AA:BB:CC:DD:EE:02",
                                        GameId: gameId,
                                        PlayedAtUtc: now.AddMinutes(-5)),
                                cancellationToken: ct);

                        // Both tied plays receive rank 1; newer timestamp sorts first on the leaderboard.
                        Assert.Equal(1, first.RankingAtTimeOfScoring);
                        Assert.Equal(1, second.RankingAtTimeOfScoring);

                        PagedHighscoreResponse leaderboard = await this._client.QueryAsync(
                                query: new HighscoreQueryRequest(
                                        GameId: gameId,
                                        LastHours: 5),
                                cancellationToken: ct);

                        Assert.Equal(2, leaderboard.TotalCount);
                        Assert.Equal("NewerTie", leaderboard.Items[0].Name);

                        // Soft-deleting by game hides all entries from subsequent queries.
                        await this._client.DeleteByGameIdAsync(gameId, ct);

                        PagedHighscoreResponse afterDelete = await this._client.QueryAsync(
                                query: new HighscoreQueryRequest(
                                        GameId: gameId,
                                        LastHours: 5),
                                cancellationToken: ct);

                        Assert.Equal(0, afterDelete.TotalCount);
                }

                /// <summary>
                /// Verifies concurrent POST submissions are serialized cleanly by the write gate without SQLite busy errors.
                /// </summary>
                [Fact]
                public async Task ConcurrentSubmissions_AreSerializedSafelyWithoutSqliteBusy()
                {
                        // Submit 12 concurrent highscores simultaneously against the single-writer SQLite store.
                        CancellationToken ct = TestContext.Current.CancellationToken;
                        Guid gameId = Guid.NewGuid();

                        IEnumerable<Task<HighscoreResponse>> tasks = Enumerable.Range(1, 12).Select(i =>
                                this._client.RecordAsync(
                                        request: new RecordHighscoreRequest(
                                                Id: Guid.NewGuid(),
                                                Name: $"Player{i}",
                                                Age: 10 + i,
                                                Score: i * 10,
                                                SenderMacAddress: $"AA:BB:CC:DD:EE:{i:X2}",
                                                GameId: gameId,
                                                PlayedAtUtc: DateTimeOffset.UtcNow),
                                        cancellationToken: ct));

                        HighscoreResponse[] results = await Task.WhenAll(tasks);
                        Assert.Equal(12, results.Length);

                        PagedHighscoreResponse leaderboard = await this._client.QueryAsync(
                                query: new HighscoreQueryRequest(
                                        GameId: gameId,
                                        LastHours: 1),
                                cancellationToken: ct);

                        Assert.Equal(12, leaderboard.TotalCount);
                        Assert.Equal(120, leaderboard.Items[0].Score);
                }

                [Fact]
                public async Task ScreenSyncStore_PrunesOnlySyncedEntriesOlderThanRetentionWindow()
                {
                        CancellationToken ct = TestContext.Current.CancellationToken;
                        Guid gameId = Guid.NewGuid();
                        DateTimeOffset now = DateTimeOffset.UtcNow;

                        using IServiceScope scope = this._factory.Services.CreateScope();
                        IScreenSyncStore syncStore = scope.ServiceProvider.GetRequiredService<IScreenSyncStore>();

                        HighscoreEntity oldSynced = HighscoreEntity.Create(
                                id: Guid.NewGuid(),
                                name: "OldSynced",
                                age: 12,
                                score: 100,
                                senderMacAddress: "AA:BB:CC:DD:EE:01",
                                gameId: gameId,
                                playedAtUtc: now.AddDays(-10));

                        HighscoreEntity recentSynced = HighscoreEntity.Create(
                                id: Guid.NewGuid(),
                                name: "RecentSynced",
                                age: 12,
                                score: 200,
                                senderMacAddress: "AA:BB:CC:DD:EE:01",
                                gameId: gameId,
                                playedAtUtc: now.AddDays(-2));

                        await syncStore.ApplySyncedBatchAsync([oldSynced, recentSynced], ct);
                        int pruned = await syncStore.PruneExpiredSyncedEntriesAsync(now.AddDays(-7), ct);

                        Assert.Equal(1, pruned);
                }

                [Fact]
                public async Task SdkClient_FallsBackToScreenWhenHubReturns503()
                {
                        CancellationToken ct = TestContext.Current.CancellationToken;
                        using HttpClient failingHub = new(new Always503Handler())
                        {
                                BaseAddress = new Uri("http://localhost:59999/")
                        };
                        HttpClient screenClient = this._factory.CreateClient();

                        HighscoreServiceClient resilientClient = new(
                                primaryHttpClient: failingHub,
                                fallbackHttpClient: screenClient,
                                hubTimeoutSeconds: 2);

                        HighscoreResponse recorded = await resilientClient.RecordAsync(
                                request: new RecordHighscoreRequest(
                                        Id: null,
                                        Name: "FailoverPlayer",
                                        Age: 15,
                                        Score: 300,
                                        SenderMacAddress: "AA:BB:CC:DD:EE:99",
                                        GameId: Guid.NewGuid()),
                                cancellationToken: ct);

                        Assert.Equal("FailoverPlayer", recorded.Name);
                }
        }
}
