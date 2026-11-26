namespace ContentService.Sdk.Models;

/// <summary>
/// Request payload for creating a new exhibition layout template in ContentService.
/// </summary>
public sealed record class CreateContentTemplateRequest
{
        /// <summary>
        /// Gets the display name for the new template.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Gets the Blazor UI component category targeted by this template.
        /// </summary>
        public required ContentTemplateComponentType ComponentType { get; init; }

        /// <summary>
        /// Gets the raw JSON layout schema (<c>JsonUiNode</c> tree) for the template.
        /// </summary>
        public required string SchemaJson { get; init; }

        /// <summary>
        /// Gets an optional audit actor identifier overriding the configured default actor.
        /// </summary>
        public string? ActorId { get; init; }
}
