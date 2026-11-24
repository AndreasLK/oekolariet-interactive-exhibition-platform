namespace ContentService.Sdk.Models;

/// <summary>
/// Payload for creating a new versioned exhibition template in ContentService.
/// </summary>
public sealed record class CreateContentTemplateRequest
{
        /// <summary>
        /// Gets the unique human-readable template name.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Gets the target Blazor UI component category for this template.
        /// </summary>
        public required ContentTemplateComponentType ComponentType { get; init; }

        /// <summary>
        /// Gets the raw JSON schema payload defining layout structure and image references.
        /// </summary>
        public required string SchemaJson { get; init; }

        /// <summary>
        /// Gets an optional actor identifier overriding <see cref="Options.ContentServiceClientOptions.DefaultActorId"/> for audit attribution.
        /// </summary>
        public string? ActorId { get; init; }
}

