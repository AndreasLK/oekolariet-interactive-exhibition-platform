using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using HighscoreService.Application.Interfaces;
using HighscoreService.Application.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HighscoreService.Screen.Infrastructure.Sync
{
        /// <summary>
        /// Broadcasts this screen's API address over UDP and tracks active LAN peers for hubless sync.
        /// </summary>
        /// <param name="scopeFactory">Service scope factory.</param>
        /// <param name="options">Screen sync options.</param>
        /// <param name="logger">Logger instance.</param>
        public sealed class UdpPeerDiscoveryService(
                IServiceScopeFactory scopeFactory,
                IOptions<ScreenSyncOptions> options,
                ILogger<UdpPeerDiscoveryService> logger) : BackgroundService
        {
                private const int PEER_TTL_SECONDS = 120;

                private readonly ConcurrentDictionary<string, DiscoveredPeer> _peers = new(StringComparer.OrdinalIgnoreCase);
                private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
                private readonly ScreenSyncOptions _options = options.Value;
                private readonly ILogger<UdpPeerDiscoveryService> _logger = logger;

                /// <summary>
                /// Returns currently active LAN peers discovered within the TTL window.
                /// </summary>
                /// <returns>Active peer records.</returns>
                public IReadOnlyList<DiscoveredPeer> GetActivePeers()
                {
                        DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddSeconds(-PEER_TTL_SECONDS);
                        return [.. this._peers.Values.Where(p => p.LastSeenUtc >= cutoff)];
                }

                /// <inheritdoc />
                protected override async Task ExecuteAsync(CancellationToken stoppingToken)
                {
                        if (!this._options.EnableUdpDiscovery)
                        {
                                return;
                        }

                        Task listenTask = this.ListenLoopAsync(stoppingToken);
                        Task broadcastTask = this.BroadcastLoopAsync(stoppingToken);

                        await Task.WhenAll(listenTask, broadcastTask);
                }

                /// <summary>
                /// Listens on all network interfaces (<c>0.0.0.0</c>) for UDP peer discovery heartbeats.
                /// </summary>
                /// <param name="stoppingToken">Shutdown cancellation token.</param>
                private async Task ListenLoopAsync(CancellationToken stoppingToken)
                {
                        try
                        {
                                // NON-STANDARD DECISION: Bind directly to IPAddress.Any (0.0.0.0) with ReuseAddress
                                // and broadcast to 255.255.255.255 instead of mDNS/DNS-SD or a central registry.
                                // Pros:
                                // - Zero-config peer discovery when the central Hub, DNS, or internet is unavailable.
                                // - Works across any local NIC without hardcoding interface IPs.
                                // Cons:
                                // - Limited to a single Layer-2 broadcast domain (does not cross routers/VLANs).
                                // - Unauthenticated LAN packets; HandleIncomingPacket must validate URLs strictly.
                                using UdpClient listener = new();
                                listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                                listener.Client.Bind(new IPEndPoint(IPAddress.Any, this._options.UdpDiscoveryPort));

                                while (!stoppingToken.IsCancellationRequested)
                                {
                                        UdpReceiveResult packet = await listener.ReceiveAsync(stoppingToken);
                                        this.HandleIncomingPacket(packet.Buffer);
                                }
                        }
                        catch (OperationCanceledException)
                        {
                                // Normal shutdown.
                        }
                        catch (Exception ex)
                        {
                                this._logger.LogWarning(ex, "UDP peer discovery listener stopped unexpectedly.");
                        }
                }

                private async Task BroadcastLoopAsync(CancellationToken stoppingToken)
                {
                        if (string.IsNullOrWhiteSpace(this._options.AdvertisedBaseUrl))
                        {
                                return;
                        }

                        TimeSpan interval = TimeSpan.FromSeconds(this._options.SyncIntervalSeconds);

                        while (!stoppingToken.IsCancellationRequested)
                        {
                                await this.SendHeartbeatAsync(stoppingToken);
                                await Task.Delay(interval, stoppingToken);
                        }
                }

                private async Task SendHeartbeatAsync(CancellationToken cancellationToken)
                {
                        try
                        {
                                using IServiceScope scope = this._scopeFactory.CreateScope();
                                IScreenSyncStore syncStore = scope.ServiceProvider.GetRequiredService<IScreenSyncStore>();
                                long maxSeq = await syncStore.GetMaxLocalSequenceAsync(cancellationToken);

                                string json = JsonSerializer.Serialize(new PeerHeartbeat(
                                        NodeId: this._options.NodeId,
                                        ApiBaseUrl: this._options.AdvertisedBaseUrl!,
                                        LatestSequence: maxSeq));

                                byte[] bytes = Encoding.UTF8.GetBytes(json);
                                using UdpClient sender = new();
                                sender.EnableBroadcast = true;
                                await sender.SendAsync(
                                        datagram: bytes,
                                        endPoint: new IPEndPoint(IPAddress.Broadcast, this._options.UdpDiscoveryPort),
                                        cancellationToken: cancellationToken);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                                this._logger.LogDebug(ex, "Failed to send UDP discovery heartbeat.");
                        }
                }

                /// <summary>
                /// Parses and validates an incoming UDP heartbeat packet, updating the active peer table.
                /// </summary>
                /// <param name="buffer">Raw UDP datagram payload.</param>
                internal void HandleIncomingPacket(byte[] buffer)
                {
                        try
                        {
                                PeerHeartbeat? heartbeat = JsonSerializer.Deserialize<PeerHeartbeat>(buffer);
                                if (heartbeat is null || string.IsNullOrWhiteSpace(heartbeat.ApiBaseUrl))
                                {
                                        return;
                                }

                                // Ignore our own broadcast echoes on 0.0.0.0.
                                if (string.Equals(heartbeat.NodeId, this._options.NodeId, StringComparison.OrdinalIgnoreCase))
                                {
                                        return;
                                }

                                // Reject non-HTTP/HTTPS or malformed URLs from untrusted LAN broadcasts.
                                string normalizedUrl = heartbeat.ApiBaseUrl.Trim().TrimEnd('/');
                                bool isValidHttpUrl = Uri.TryCreate(normalizedUrl, UriKind.Absolute, out Uri? parsed)
                                        && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps);
                                if (!isValidHttpUrl)
                                {
                                        return;
                                }

                                this._peers[heartbeat.NodeId] = new DiscoveredPeer(
                                        NodeId: heartbeat.NodeId,
                                        ApiBaseUrl: normalizedUrl,
                                        LatestSequence: heartbeat.LatestSequence,
                                        LastSeenUtc: DateTimeOffset.UtcNow);
                        }
                        catch
                        {
                                // Ignore malformed UDP packets on the shared LAN.
                        }
                }
        }
}

