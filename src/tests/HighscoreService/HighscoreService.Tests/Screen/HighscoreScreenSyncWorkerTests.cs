using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HighscoreService.Application.Interfaces;
using HighscoreService.Application.Models;
using HighscoreService.Application.Options;
using HighscoreService.Domain.Entities;
using HighscoreService.Screen.Infrastructure.Sync;
using HighscoreService.Tests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HighscoreService.Tests.Screen
{
        /// <summary>
        /// Socket-free unit tests for <see cref="HighscoreScreenSyncWorker"/> Hub-to-LAN fallback behavior.
        /// </summary>
        public sealed class HighscoreScreenSyncWorkerTests
        {
                /// <summary>
                /// Ensures that when the Hub is unreachable, the sync worker pulls deltas from discovered LAN peers.
                /// </summary>
                [Fact]
                public async Task RunSyncCycleAsync_FallsBackToLanPeers_WhenHubIsUnreachable()
                {
                        // Arrange
                        StubScreenSyncStore store = new();
                        ServiceCollection services = new();
                        services.AddScoped<IScreenSyncStore>(_ => store);
                        using ServiceProvider provider = services.BuildServiceProvider();

                        ScreenSyncOptions options = new()
                        {
                                NodeId = "screen-1",
                                CentralHubBaseUrl = "http://hub.offline.local:8080",
                                RetentionDays = 7,
                                SyncBatchSize = 50
                        };

                        UdpPeerDiscoveryService peerDiscovery = new(
                                provider.GetRequiredService<IServiceScopeFactory>(),
                                Options.Create(options),
                                NullLogger<UdpPeerDiscoveryService>.Instance);

                        // Inject a peer heartbeat directly without binding UDP sockets.
                        string heartbeatJson = JsonSerializer.Serialize(new PeerHeartbeat("screen-2", "http://screen-2.local:8080", 5));
                        peerDiscovery.HandleIncomingPacket(Encoding.UTF8.GetBytes(heartbeatJson));

                        Guid gameId = Guid.NewGuid();
                        StubHttpClientFactory httpFactory = new(req =>
                        {
                                // Simulate Hub down while LAN peer returns one missed score.
                                if (req.RequestUri!.Host.Equals("hub.offline.local", StringComparison.OrdinalIgnoreCase))
                                {
                                        throw new HttpRequestException("Hub unreachable");
                                }

                                HighscoreResult peerScore = new(Guid.NewGuid(), "LanPlayer", 10, 250, "AA:BB:CC:DD:EE:02", gameId, DateTimeOffset.UtcNow, 1, false, false, null, 5);
                                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { peerScore }) });
                        });

                        HighscoreScreenSyncWorker sut = new(
                                provider.GetRequiredService<IServiceScopeFactory>(),
                                httpFactory,
                                peerDiscovery,
                                Options.Create(options),
                                NullLogger<HighscoreScreenSyncWorker>.Instance);

                        // Act
                        await sut.RunSyncCycleAsync(TestContext.Current.CancellationToken);

                        // Assert
                        HighscoreEntity synced = Assert.Single(store.SyncedEntities);
                        Assert.Equal("LanPlayer", synced.Name);
                        Assert.Equal(5, store.LastWatermark);
                }

                /// <summary>
                /// Ensures that a Hub <c>401 Unauthorized</c> response triggers zero-touch enrollment without crashing.
                /// </summary>
                [Fact]
                public async Task RunSyncCycleAsync_AutoEnrolls_WhenHubReturns401()
                {
                        // Arrange
                        string tempIdentityPath = Path.Combine(Path.GetTempPath(), $"worker-id-{Guid.NewGuid():N}.json");
                        try
                        {
                                StubScreenSyncStore store = new();
                                ServiceCollection services = new();
                                services.AddScoped<IScreenSyncStore>(_ => store);
                                using ServiceProvider provider = services.BuildServiceProvider();

                                ScreenSyncOptions options = new()
                                {
                                        NodeId = "screen-1",
                                        CentralHubBaseUrl = "http://hub.local:8080",
                                        IdentityFilePath = tempIdentityPath,
                                        RetentionDays = 7,
                                        SyncBatchSize = 50
                                };

                                UdpPeerDiscoveryService peerDiscovery = new(
                                        provider.GetRequiredService<IServiceScopeFactory>(),
                                        Options.Create(options),
                                        NullLogger<UdpPeerDiscoveryService>.Instance);

                                bool enrollCalled = false;
                                StubHttpClientFactory httpFactory = new(req =>
                                {
                                        // Simulate 401 on tombstone pull and 201 Created on /api/v1/screens/enroll.
                                        if (req.RequestUri!.AbsolutePath.EndsWith("/screens/enroll", StringComparison.Ordinal))
                                        {
                                                enrollCalled = true;
                                                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created));
                                        }

                                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
                                });

                                ScreenIdentityStore identityStore = new(Options.Create(options));
                                HighscoreScreenSyncWorker sut = new(
                                        provider.GetRequiredService<IServiceScopeFactory>(),
                                        httpFactory,
                                        peerDiscovery,
                                        Options.Create(options),
                                        NullLogger<HighscoreScreenSyncWorker>.Instance,
                                        identityStore);

                                // Act
                                await sut.RunSyncCycleAsync(TestContext.Current.CancellationToken);

                                // Assert
                                Assert.True(enrollCalled);
                        }
                        finally
                        {
                                if (File.Exists(tempIdentityPath))
                                {
                                        File.Delete(tempIdentityPath);
                                }
                        }
                }

                /// <summary>
                /// Ensures a malformed <see cref="ScreenSyncOptions.CentralHubBaseUrl"/> falls back to LAN peers cleanly.
                /// </summary>
                [Fact]
                public async Task RunSyncCycleAsync_FallsBackToLanPeers_WhenHubUrlIsMalformed()
                {
                        // Arrange
                        StubScreenSyncStore store = new();
                        ServiceCollection services = new();
                        services.AddScoped<IScreenSyncStore>(_ => store);
                        using ServiceProvider provider = services.BuildServiceProvider();

                        ScreenSyncOptions options = new()
                        {
                                NodeId = "screen-1",
                                CentralHubBaseUrl = "not-a-valid-uri",
                                RetentionDays = 7,
                                SyncBatchSize = 50
                        };

                        UdpPeerDiscoveryService peerDiscovery = new(
                                provider.GetRequiredService<IServiceScopeFactory>(),
                                Options.Create(options),
                                NullLogger<UdpPeerDiscoveryService>.Instance);

                        string heartbeatJson = JsonSerializer.Serialize(new PeerHeartbeat("screen-2", "http://screen-2.local:8080", 9));
                        peerDiscovery.HandleIncomingPacket(Encoding.UTF8.GetBytes(heartbeatJson));

                        Guid gameId = Guid.NewGuid();
                        StubHttpClientFactory httpFactory = new(_ =>
                        {
                                HighscoreResult peerScore = new(Guid.NewGuid(), "PeerScore", 12, 500, "AA:BB:CC:DD:EE:02", gameId, DateTimeOffset.UtcNow, 1, false, false, null, 9);
                                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { peerScore }) });
                        });

                        HighscoreScreenSyncWorker sut = new(
                                provider.GetRequiredService<IServiceScopeFactory>(),
                                httpFactory,
                                peerDiscovery,
                                Options.Create(options),
                                NullLogger<HighscoreScreenSyncWorker>.Instance);

                        // Act
                        await sut.RunSyncCycleAsync(TestContext.Current.CancellationToken);

                        // Assert
                        HighscoreEntity synced = Assert.Single(store.SyncedEntities);
                        Assert.Equal("PeerScore", synced.Name);
                        Assert.Equal(9, store.LastWatermark);
                }
        }
}
