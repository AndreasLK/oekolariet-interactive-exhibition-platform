namespace HighscoreService.Screen.Infrastructure.Persistence
{
        /// <summary>
        /// Tracks local highscore writes or tombstones waiting to be pushed to the central Hub.
        /// </summary>
        public sealed class ScreenOutboxEntry
        {
                /// <summary>
                /// Gets or sets the highscore identifier pending synchronization.
                /// </summary>
                public Guid HighscoreId { get; set; }

                /// <summary>
                /// Gets or sets the monotonic local sequence at the time this entry was queued.
                /// </summary>
                public long QueuedSequence { get; set; }

                /// <summary>
                /// Gets or sets the UTC timestamp when queued.
                /// </summary>
                public DateTimeOffset QueuedAtUtc { get; set; }
        }
}

