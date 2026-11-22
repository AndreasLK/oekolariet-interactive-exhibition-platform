namespace ContentService.Sdk.Options;

/// <summary>
/// Configuration options for connecting to the ContentService HTTP API.
/// </summary>
public sealed class ContentServiceClientOptions
{
        /// <summary>
        /// Gets or sets the absolute base URI of the ContentService HTTP API (e.g., <c>https://content.example.com</c> or <c>http://contentservice:8080</c>).
        /// </summary>
        public Uri? BaseAddress { get; set; }

        /// <summary>
        /// Gets or sets an optional default actor identifier sent via <c>X-Actor-Id</c> when a request does not specify an actor.
        /// </summary>
        public string? DefaultActorId { get; set; }

        /// <summary>
        /// Gets or sets an optional HTTP request timeout applied to the ContentService <see cref="HttpClient"/>.
        /// </summary>
        public TimeSpan? RequestTimeout { get; set; }
}

