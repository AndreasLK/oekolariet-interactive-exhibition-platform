namespace HighscoreService.Hub.Api.Options
{
        /// <summary>
        /// Configuration options controlling Cross-Origin Resource Sharing (CORS) on the Hub API.
        /// </summary>
        /// <remarks>
        /// Named <see cref="HubCorsOptions"/> instead of <c>CorsOptions</c> to avoid colliding with
        /// <see cref="Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions"/> in <c>Program.cs</c>.
        /// </remarks>
        public sealed class HubCorsOptions
        {
                /// <summary>
                /// Configuration section name for Hub CORS settings.
                /// </summary>
                public const string SECTION_NAME = "Cors";

                /// <summary>
                /// Explicit browser origins permitted to call the Hub API cross-origin
                /// (for example, an external Blazor WebAssembly frontend).
                /// </summary>
                /// <remarks>
                /// Server-to-server callers (<c>HighscoreScreenSyncWorker</c> and <c>HttpHighscoreEndpointClient</c>)
                /// and same-origin reverse-proxy traffic do not require CORS.
                /// </remarks>
                public string[] AllowedOrigins { get; init; } = [];

                /// <summary>
                /// Whether browsers are allowed to include credentials (cookies or authorization headers)
                /// on cross-origin requests. Ignored unless <see cref="AllowedOrigins"/> lists explicit origins.
                /// </summary>
                public bool AllowCredentials { get; init; }
        }
}

