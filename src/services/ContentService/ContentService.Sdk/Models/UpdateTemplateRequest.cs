namespace ContentService.Sdk.Models;

/// <summary>
/// Request payload for updating an existing exhibition layout template in ContentService.
/// </summary>
public sealed record class UpdateContentTemplateRequest
{
        /// <summary>
        /// Gets the updated display name for the template.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Gets the updated raw JSON layout schema (<c>JsonUiNode</c> tree).
        /// </summary>
        public required string SchemaJson { get; init; }

        /// <summary>
        /// Gets the expected optimistic concurrency stamp observed when the template was loaded.
        /// </summary>
        public required Guid ExpectedConcurrencyStamp { get; init; }

        /// <summary>
        /// Gets an optional audit actor identifier overriding the configured default actor.
        /// </summary>
        public string? ActorId { get; init; }
}
