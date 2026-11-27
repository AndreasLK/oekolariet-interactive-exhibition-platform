using System.ComponentModel.DataAnnotations;

namespace HighscoreService.Application.Options
{
        /// <summary>
        /// Configuration options for slim Screen servers (selective sync, 7-day retention, and UDP LAN discovery).
        /// </summary>
        /// <remarks>
        /// In the exhibition topology, each Screen server syncs Hub-first using <see cref="CentralHubBaseUrl"/> and
        /// its auto-generated <see cref="IdentityFilePath"/> credential. If the Hub is unreachable, it falls back
        /// to discovering peer screens via UDP on <see cref="UdpDiscoveryPort"/> and pulling deltas from their
        /// <see cref="AdvertisedBaseUrl"/> over the isolated LAN.
        /// </remarks>
        public sealed class ScreenSyncOptions
        {
                /// <summary>
                /// Configuration section name in appsettings.json.
                /// </summary>
                public const string SECTION_NAME = "ScreenSync";

                /// <summary>
                /// Gets or sets the unique node identifier for this screen server (defaults to machine name when empty).
                /// </summary>
                public string NodeId { get; set; } = Environment.MachineName;

                /// <summary>
                /// Gets or sets the central Hub API base URL (priority sync target).
                /// </summary>
                public string? CentralHubBaseUrl { get; set; }

                /// <summary>
                /// Gets or sets the HTTP base URL advertised by this screen to LAN peers for delta pulls.
                /// </summary>
                public string? AdvertisedBaseUrl { get; set; }

                /// <summary>
                /// Gets or sets the game identifiers active on this screen's UI server (empty syncs all games).
                /// </summary>
                public List<Guid> SubscribedGameIds { get; set; } = [];

                /// <summary>
                /// Gets or sets the number of days to retain already-synced local entries before pruning.
                /// </summary>
                [Range(1, 365)]
                public int RetentionDays { get; set; } = 7;

                /// <summary>
                /// Gets or sets a value indicating whether UDP LAN peer discovery is enabled.
                /// </summary>
                public bool EnableUdpDiscovery { get; set; } = true;

                /// <summary>
                /// Gets or sets the UDP port used for LAN peer discovery broadcasts.
                /// </summary>
                [Range(1024, 65535)]
                public int UdpDiscoveryPort { get; set; } = 47890;

                /// <summary>
                /// Gets or sets the background sync interval in seconds.
                /// </summary>
                [Range(5, 3600)]
                public int SyncIntervalSeconds { get; set; } = 30;

                /// <summary>
                /// Gets or sets the maximum batch size per sync HTTP request.
                /// </summary>
                [Range(10, 1000)]
                public int SyncBatchSize { get; set; } = 100;

                /// <summary>
                /// Gets or sets the optional file path where this screen persists its auto-generated identity credential.
                /// </summary>
                public string? IdentityFilePath { get; set; }
        }
}

