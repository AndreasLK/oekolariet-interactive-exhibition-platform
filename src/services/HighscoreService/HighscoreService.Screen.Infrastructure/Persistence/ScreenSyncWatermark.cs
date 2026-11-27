namespace HighscoreService.Screen.Infrastructure.Persistence
{
        /// <summary>
        /// Stores the last pulled <c>SyncSequence</c> watermark per source node and game.
        /// </summary>
        public sealed class ScreenSyncWatermark
        {
                /// <summary>
                /// Gets or sets the source node identifier (e.g. "hub" or a LAN peer NodeId).
                /// </summary>
                public string SourceNodeId { get; set; } = string.Empty;

                /// <summary>
                /// Gets or sets the game identifier (or <see cref="Guid.Empty"/> for global tombstones).
                /// </summary>
                public Guid GameId { get; set; }

                /// <summary>
                /// Gets or sets the last pulled monotonic sequence.
                /// </summary>
                public long LastSequence { get; set; }
        }
}

