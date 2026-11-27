using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HighscoreService.Application.Common;
using HighscoreService.Application.Interfaces;
using HighscoreService.Application.Models;
using HighscoreService.Application.Options;
using HighscoreService.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HighscoreService.Screen.Infrastructure.Sync
{
        /// <summary>
        /// Coordinates Hub-priority outbox push, per-GameId 7-day delta pull, LAN peer fallback, and 7-day retention pruning.
        /// </summary>
        /// <param name="scopeFactory">Service scope factory.</param>
        /// <param name="httpClientFactory">HTTP client factory.</param>
        /// <param name="peerDiscovery">UDP LAN peer discovery service.</param>
        /// <param name="options">Screen sync options.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="identityStore">Optional zero-touch screen identity store for Hub bearer auth.</param>
        public sealed class HighscoreScreenSyncWorker(
                IServiceScopeFactory scopeFactory,
                IHttpClientFactory httpClientFactory,
                UdpPeerDiscoveryService peerDiscovery,
                IOptions<ScreenSyncOptions> options,
                ILogger<HighscoreScreenSyncWorker> logger,
                ScreenIdentityStore? identityStore = null) : BackgroundService
        {
                private const string HUB_SOURCE_ID = "hub";

                private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
                private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
                private readonly UdpPeerDiscoveryService _peerDiscovery = peerDiscovery;
                private readonly ScreenSyncOptions _options = options.Value;
                private readonly ILogger<HighscoreScreenSyncWorker> _logger = logger;
                private readonly ScreenIdentityStore? _identityStore = identityStore;

                /// <inheritdoc />
                protected override async Task ExecuteAsync(CancellationToken stoppingToken)
                {
                        TimeSpan interval = TimeSpan.FromSeconds(this._options.SyncIntervalSeconds);

                        while (!stoppingToken.IsCancellationRequested)
                        {
                                await this.RunSyncCycleAsync(stoppingToken);
                                await Task.Delay(interval, stoppingToken);
                        }
                }

                /// <summary>
                /// Executes one full sync cycle: Hub-first outbox push + delta pull, LAN peer fallback if Hub is
                /// unavailable, and 7-day retention pruning.
                /// </summary>
                /// <param name="cancellationToken">Cancellation token.</param>
                internal async Task RunSyncCycleAsync(CancellationToken cancellationToken)
                {
                        using IServiceScope scope = this._scopeFactory.CreateScope();
                        IScreenSyncStore syncStore = scope.ServiceProvider.GetRequiredService<IScreenSyncStore>();

                        // NON-STANDARD DECISION: Hub-priority sync with automatic LAN peer fallback.
                        // Standard cloud architectures treat the central Hub as a hard dependency.
                        // Pros: Exhibition screens continue sharing live leaderboards even if the internet,
                        // reverse proxy, or central Hub server is offline.
                        // Cons: Eventual consistency during partitions; rankings computed offline are best-effort.
                        bool hubSynced = await this.TrySyncWithHubAsync(syncStore, cancellationToken);
                        if (!hubSynced)
                        {
                                await this.PullFromLanPeersAsync(syncStore, cancellationToken);
                        }

                        DateTimeOffset retentionCutoff = DateTimeOffset.UtcNow.AddDays(-this._options.RetentionDays);
                        await syncStore.PruneExpiredSyncedEntriesAsync(retentionCutoff, cancellationToken);
                }

                /// <summary>
                /// Attempts to push local outbox entries and pull tombstones/deltas from the central Hub.
                /// </summary>
                /// <param name="syncStore">Screen sync store.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns><see langword="true"/> if Hub sync succeeded; otherwise <see langword="false"/>.</returns>
                private async Task<bool> TrySyncWithHubAsync(
                        IScreenSyncStore syncStore,
                        CancellationToken cancellationToken)
                {
                        if (string.IsNullOrWhiteSpace(this._options.CentralHubBaseUrl))
                        {
                                return false;
                        }

                        string hubBase = this._options.CentralHubBaseUrl.Trim().TrimEnd('/');
                        if (!Uri.TryCreate(hubBase, UriKind.Absolute, out _))
                        {
                                // Guard against malformed config crashing the BackgroundService host; fall back to LAN peers.
                                this._logger.LogWarning("CentralHubBaseUrl '{Url}' is not a valid absolute URI; falling back to LAN peers.", hubBase);
                                return false;
                        }

                        try
                        {
                                string? bearerToken = await this.GetScreenBearerTokenAsync(cancellationToken);
                                await this.PushOutboxToHubAsync(syncStore, hubBase, bearerToken, cancellationToken);
                                await this.PullHubTombstonesAsync(syncStore, hubBase, bearerToken, cancellationToken);
                                await this.PullSelectiveDeltasAsync(syncStore, HUB_SOURCE_ID, hubBase, bearerToken, cancellationToken);
                                return true;
                        }
                        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
                        {
                                // WHY: Auto-enroll on first 401 so non-technical installers never copy API keys or edit .env files.
                                await this.TryEnrollWithHubAsync(hubBase, cancellationToken);
                                return false;
                        }
                        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                        {
                                this._logger.LogDebug(ex, "Central Hub unreachable; falling back to LAN peers.");
                                return false;
                        }
                }

                private async Task<string?> GetScreenBearerTokenAsync(CancellationToken cancellationToken)
                {
                        // Identity store is optional in socket-free unit tests; in production it supplies 'scr_<id>.<secret>'.
                        if (this._identityStore is null)
                        {
                                return null;
                        }

                        ScreenIdentity identity = await this._identityStore.GetOrCreateAsync(cancellationToken);
                        return identity.ToBearerToken();
                }

                private async Task TryEnrollWithHubAsync(string hubBaseUrl, CancellationToken cancellationToken)
                {
                        if (this._identityStore is null)
                        {
                                return;
                        }

                        try
                        {
                                ScreenIdentity identity = await this._identityStore.GetOrCreateAsync(cancellationToken);
                                HttpClient client = this._httpClientFactory.CreateClient("ScreenSync");
                                var payload = new
                                {
                                        identity.ScreenId,
                                        this._options.NodeId,
                                        MacAddress = (string?)null,
                                        identity.Secret
                                };

                                using HttpResponseMessage response = await client.PostAsJsonAsync(
                                        requestUri: $"{hubBaseUrl}/api/v1/screens/enroll",
                                        value: payload,
                                        cancellationToken: cancellationToken);

                                this._logger.LogWarning(
                                        "Screen '{NodeId}' ({ScreenId}) enrolled/checked with Hub (HTTP {StatusCode}) and awaits admin approval; falling back to LAN peers.",
                                        this._options.NodeId,
                                        identity.ScreenId,
                                        (int)response.StatusCode);
                        }
                        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                        {
                                this._logger.LogDebug(ex, "Screen auto-enrollment attempt failed; will retry on next sync cycle.");
                        }
                }

                private HttpClient CreateClient(string? bearerToken)
                {
                        HttpClient client = this._httpClientFactory.CreateClient("ScreenSync");
                        if (!string.IsNullOrWhiteSpace(bearerToken))
                        {
                                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
                        }

                        return client;
                }

                private async Task PushOutboxToHubAsync(
                        IScreenSyncStore syncStore,
                        string hubBaseUrl,
                        string? bearerToken,
                        CancellationToken cancellationToken)
                {
                        IReadOnlyList<(HighscoreEntity Entity, long OutboxSequence)> pending =
                                await syncStore.GetPendingOutboxAsync(this._options.SyncBatchSize, cancellationToken);

                        if (pending.Count == 0)
                        {
                                return;
                        }

                        HttpClient client = this.CreateClient(bearerToken);
                        List<HighscoreResult> payload = [.. pending.Select(p => HighscoreResultMapper.ToResult(p.Entity))];

                        using HttpResponseMessage response = await client.PostAsJsonAsync(
                                requestUri: $"{hubBaseUrl}/api/v1/highscores/sync/batch",
                                value: payload,
                                cancellationToken: cancellationToken);

                        response.EnsureSuccessStatusCode();

                        List<(Guid, long)> acked = [.. pending.Select(p => (p.Entity.Id, p.OutboxSequence))];
                        await syncStore.MarkOutboxSyncedAsync(acked, cancellationToken);
                }

                private async Task PullHubTombstonesAsync(
                        IScreenSyncStore syncStore,
                        string hubBaseUrl,
                        string? bearerToken,
                        CancellationToken cancellationToken)
                {
                        long watermark = await syncStore.GetPullWatermarkAsync(HUB_SOURCE_ID, Guid.Empty, cancellationToken);
                        HttpClient client = this.CreateClient(bearerToken);

                        List<TombstonePayload>? tombstones = await client.GetFromJsonAsync<List<TombstonePayload>>(
                                requestUri: $"{hubBaseUrl}/api/v1/highscores/sync/tombstones?afterSequence={watermark}",
                                cancellationToken: cancellationToken);

                        if (tombstones is null || tombstones.Count == 0)
                        {
                                return;
                        }

                        List<GameDeletionTombstone> domainTombstones =
                        [
                                .. tombstones.Select(t => GameDeletionTombstone.Create(t.GameId, t.DeletedAtUtc, t.SyncSequence))
                        ];

                        await syncStore.ApplyGameDeletionTombstonesAsync(domainTombstones, cancellationToken);
                        await syncStore.SavePullWatermarkAsync(HUB_SOURCE_ID, Guid.Empty, tombstones.Max(t => t.SyncSequence), cancellationToken);
                }

                private async Task PullFromLanPeersAsync(
                        IScreenSyncStore syncStore,
                        CancellationToken cancellationToken)
                {
                        // Screen-to-Screen LAN fallback is intentionally unauthenticated (bearerToken: null) and read-only
                        // (GET /sync/delta only). Screen servers live on an isolated exhibition LAN/VLAN and never exchange
                        // scr_ credentials or accept deletes/tombstones from peers; authoritative deletes come from the Hub.
                        foreach (DiscoveredPeer peer in this._peerDiscovery.GetActivePeers())
                        {
                                try
                                {
                                        await this.PullSelectiveDeltasAsync(
                                                syncStore: syncStore,
                                                sourceNodeId: peer.NodeId,
                                                baseUrl: peer.ApiBaseUrl.TrimEnd('/'),
                                                bearerToken: null,
                                                cancellationToken: cancellationToken);
                                }
                                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                                {
                                        if (this._logger.IsEnabled(LogLevel.Debug))
                                        {
                                                this._logger.LogDebug(ex, "LAN peer {NodeId} unreachable.", peer.NodeId);
                                        }
                                }
                        }
                }

                private async Task PullSelectiveDeltasAsync(
                        IScreenSyncStore syncStore,
                        string sourceNodeId,
                        string baseUrl,
                        string? bearerToken,
                        CancellationToken cancellationToken)
                {
                        IReadOnlyList<Guid?> targets = this._options.SubscribedGameIds.Count > 0
                                ? this._options.SubscribedGameIds.Select(id => (Guid?)id).ToList()
                                : [null];

                        DateTimeOffset retentionStart = DateTimeOffset.UtcNow.AddDays(-this._options.RetentionDays);
                        HttpClient client = this.CreateClient(bearerToken);

                        foreach (Guid? gameId in targets)
                        {
                                Guid watermarkKey = gameId ?? Guid.Empty;
                                long afterSeq = await syncStore.GetPullWatermarkAsync(sourceNodeId, watermarkKey, cancellationToken);
                                string gameParam = gameId.HasValue ? $"&gameId={gameId.Value}" : string.Empty;
                                string sinceParam = afterSeq == 0 ? $"&sinceUtc={Uri.EscapeDataString(retentionStart.ToString("O"))}" : string.Empty;
                                string url = $"{baseUrl}/api/v1/highscores/sync/delta?afterSequence={afterSeq}&batchSize={this._options.SyncBatchSize}{gameParam}{sinceParam}";

                                List<HighscoreResult>? deltas = await client.GetFromJsonAsync<List<HighscoreResult>>(url, cancellationToken);
                                if (deltas is null || deltas.Count == 0)
                                {
                                        continue;
                                }

                                List<HighscoreEntity> entities = [.. deltas.Select(ToEntity)];
                                await syncStore.ApplySyncedBatchAsync(entities, cancellationToken);
                                await syncStore.SavePullWatermarkAsync(sourceNodeId, watermarkKey, deltas.Max(d => d.SyncSequence), cancellationToken);
                        }
                }

                private static HighscoreEntity ToEntity(HighscoreResult result)
                {
                        HighscoreEntity entity = HighscoreEntity.Create(
                                id: result.Id,
                                name: result.Name,
                                age: result.Age,
                                score: result.Score,
                                senderMacAddress: result.SenderMacAddress,
                                gameId: result.GameId,
                                playedAtUtc: result.PlayedAtUtc,
                                isTimedOut: result.IsTimedOut,
                                rankingAtTimeOfScoring: result.RankingAtTimeOfScoring);

                        if (result.IsDeleted && result.DeletedAtUtc.HasValue)
                        {
                                entity.MarkDeleted(result.DeletedAtUtc.Value);
                        }

                        return entity;
                }
        }
}

