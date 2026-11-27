namespace HighscoreService.Screen.Infrastructure.Sync
{
        /// <summary>
        /// Represents a peer screen discovered on the local network via UDP broadcast.
        /// </summary>
        /// <param name="NodeId">Unique screen node identifier.</param>
        /// <param name="ApiBaseUrl">Reachable HTTP base URL of the peer's API.</param>
        /// <param name="LatestSequence">Highest local sync sequence advertised by the peer.</param>
        /// <param name="LastSeenUtc">UTC timestamp when the peer was last observed.</param>
        public sealed record DiscoveredPeer(
                string NodeId,
                string ApiBaseUrl,
                long LatestSequence,
                DateTimeOffset LastSeenUtc);
}

