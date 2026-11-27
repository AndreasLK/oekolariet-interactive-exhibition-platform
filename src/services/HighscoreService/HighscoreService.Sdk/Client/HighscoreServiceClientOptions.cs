namespace HighscoreService.Sdk.Client
{
        /// <summary>
        /// Configuration options for <see cref="HighscoreServiceClient"/> supporting Hub-first with local Screen fallback.
        /// </summary>
        public sealed class HighscoreServiceClientOptions
        {
                /// <summary>
                /// Default Hub request timeout in seconds before failing over to the local Screen service.
                /// </summary>
                public const int DEFAULT_HUB_TIMEOUT_SECONDS = 3;

                /// <summary>
                /// Gets or sets the primary central Hub base address.
                /// </summary>
                public required Uri PrimaryBaseAddress { get; set; }

                /// <summary>
                /// Gets or sets the optional local Screen fallback base address used when the Hub is unreachable.
                /// </summary>
                public Uri? FallbackBaseAddress { get; set; }

                /// <summary>
                /// Gets or sets the Hub request timeout in seconds before falling back to the local Screen service.
                /// </summary>
                public int HubTimeoutSeconds { get; set; } = DEFAULT_HUB_TIMEOUT_SECONDS;
        }
}

