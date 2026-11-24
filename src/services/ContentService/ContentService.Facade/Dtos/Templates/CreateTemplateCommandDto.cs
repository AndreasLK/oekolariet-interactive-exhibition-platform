namespace ContentService.Facade.Dtos;

/// <summary>
/// Command DTO for creating a new versioned exhibition template.
/// </summary>
public sealed record class CreateTemplateCommandDto
{
        /// <summary>
        /// Gets the display name of the new template.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Gets the immutable Blazor UI component category targeted by this template.
        /// </summary>
        public required TemplateComponentTypeDto ComponentType { get; init; }

        /// <summary>
        /// Gets the raw JSON schema payload defining layout structure and picture references.
        /// </summary>
        public required string SchemaJson { get; init; }

        /// <summary>
        /// Gets the trusted actor identifier for audit attribution.
        /// </summary>
        public required string ActorId { get; init; }
}

