namespace HighscoreService.Screen.Infrastructure.Sync
{
        /// <summary>
        /// JSON payload broadcast over UDP to advertise a Screen API's presence and sync progress.
        /// </summary>
        /// <param name="NodeId">Unique screen node identifier.</param>
        /// <param name="ApiBaseUrl">Reachable HTTP base URL of the screen's API.</param>
        /// <param name="LatestSequence">Highest local sync sequence on the advertising screen.</param>
        internal sealed record PeerHeartbeat(
                string NodeId,
                string ApiBaseUrl,
                long LatestSequence);
}

