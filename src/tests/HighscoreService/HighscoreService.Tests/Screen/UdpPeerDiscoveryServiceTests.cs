using System.Text;
using System.Text.Json;
using HighscoreService.Application.Interfaces;
using HighscoreService.Application.Options;
using HighscoreService.Screen.Infrastructure.Sync;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HighscoreService.Tests.Screen
{
        /// <summary>
        /// Socket-free unit tests for <see cref="UdpPeerDiscoveryService"/> packet parsing and validation.
        /// </summary>
        public sealed class UdpPeerDiscoveryServiceTests
        {
                /// <summary>
                /// Ensures a valid HTTP peer heartbeat is registered and its trailing slash is normalized.
                /// </summary>
                [Fact]
                public void HandleIncomingPacket_RegistersValidHttpPeer()
                {
                        // Arrange
                        UdpPeerDiscoveryService sut = CreateService("screen-1");
                        byte[] packet = SerializeHeartbeat("screen-2", "http://10.0.0.22:8080/", 42);

                        // Act
                        sut.HandleIncomingPacket(packet);

                        // Assert
                        IReadOnlyList<DiscoveredPeer> peers = sut.GetActivePeers();
                        DiscoveredPeer peer = Assert.Single(peers);
                        Assert.Equal("screen-2", peer.NodeId);
                        Assert.Equal("http://10.0.0.22:8080", peer.ApiBaseUrl);
                        Assert.Equal(42, peer.LatestSequence);
                }

                /// <summary>
                /// Ensures broadcasts originating from our own NodeId on 0.0.0.0 are ignored.
                /// </summary>
                [Fact]
                public void HandleIncomingPacket_IgnoresSelfBroadcast()
                {
                        // Arrange
                        UdpPeerDiscoveryService sut = CreateService("screen-1");
                        byte[] packet = SerializeHeartbeat("SCREEN-1", "http://10.0.0.21:8080", 10);

                        // Act
                        sut.HandleIncomingPacket(packet);

                        // Assert
                        Assert.Empty(sut.GetActivePeers());
                }

                /// <summary>
                /// Ensures malformed or non-HTTP/HTTPS URLs from untrusted LAN UDP packets are rejected.
                /// </summary>
                [Theory]
                [InlineData("not-a-url")]
                [InlineData("ftp://10.0.0.99/file")]
                [InlineData("file:///etc/passwd")]
                [InlineData("")]
                public void HandleIncomingPacket_RejectsMalformedOrNonHttpUrls(string invalidUrl)
                {
                        // Arrange
                        UdpPeerDiscoveryService sut = CreateService("screen-1");
                        byte[] packet = SerializeHeartbeat("rogue-node", invalidUrl, 1);

                        // Act
                        sut.HandleIncomingPacket(packet);

                        // Assert
                        Assert.Empty(sut.GetActivePeers());
                }

                private static UdpPeerDiscoveryService CreateService(string nodeId)
                {
                        ServiceCollection services = new();
                        ServiceProvider provider = services.BuildServiceProvider();
                        ScreenSyncOptions options = new() { NodeId = nodeId };

                        return new UdpPeerDiscoveryService(
                                scopeFactory: provider.GetRequiredService<IServiceScopeFactory>(),
                                options: Options.Create(options),
                                logger: NullLogger<UdpPeerDiscoveryService>.Instance);
                }

                private static byte[] SerializeHeartbeat(string nodeId, string apiBaseUrl, long latestSequence)
                {
                        string json = JsonSerializer.Serialize(new PeerHeartbeat(nodeId, apiBaseUrl, latestSequence));
                        return Encoding.UTF8.GetBytes(json);
                }
        }
}

