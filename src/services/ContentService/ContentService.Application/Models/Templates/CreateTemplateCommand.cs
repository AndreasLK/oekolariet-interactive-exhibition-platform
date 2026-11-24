using ContentService.Domain.Enums;

namespace ContentService.Application.Models;

/// <summary>
/// Command payload for creating a new versioned exhibition template.
/// </summary>
public sealed record class CreateTemplateCommand
{
        /// <summary>
        /// Gets the display name of the new template.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Gets the immutable Blazor UI component category targeted by this template.
        /// </summary>
        public required TemplateComponentType ComponentType { get; init; }

        /// <summary>
        /// Gets the raw JSON schema payload defining layout structure and <c>picture.{node}.{guid}</c> references.
        /// </summary>
        public required string SchemaJson { get; init; }

        /// <summary>
        /// Gets the identifier of the actor creating the template for audit logging.
        /// </summary>
        public required string ActorId { get; init; }
}

