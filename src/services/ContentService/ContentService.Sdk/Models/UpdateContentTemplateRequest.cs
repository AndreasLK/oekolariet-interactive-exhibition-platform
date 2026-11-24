namespace ContentService.Sdk.Models;

/// <summary>
/// Payload for updating an existing template's name and schema JSON with optimistic concurrency checking.
/// </summary>
public sealed record class UpdateContentTemplateRequest
{
        /// <summary>
        /// Gets the updated template name.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Gets the updated raw JSON schema payload.
        /// </summary>
        public required string SchemaJson { get; init; }

        /// <summary>
        /// Gets the optimistic concurrency token observed when the template was loaded.
        /// </summary>
        public required Guid ExpectedConcurrencyStamp { get; init; }

        /// <summary>
        /// Gets an optional actor identifier overriding <see cref="Options.ContentServiceClientOptions.DefaultActorId"/> for audit attribution.
        /// </summary>
        public string? ActorId { get; init; }
}

