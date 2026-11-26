using ContentService.Facade.Common;

namespace ContentService.Facade.Dtos;

/// <summary>
/// Identifies the Blazor UI component category targeted by a template at the facade boundary.
/// </summary>
public enum TemplateComponentTypeDto
{
        /// <summary>
        /// Renders an interactive quiz experience.
        /// </summary>
        QuizTemplate = 0,

        /// <summary>
        /// Renders an interactive game experience.
        /// </summary>
        GameTemplate = 1,

        /// <summary>
        /// Renders a static or animated informational exhibition screen.
        /// </summary>
        InfoScreen = 2,

        /// <summary>
        /// Renders a bespoke one-off exhibition component outside the standard presets.
        /// </summary>
        Custom = 3
}

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

/// <summary>
/// Command DTO for updating an existing template's name and schema JSON with optimistic concurrency checking.
/// </summary>
public sealed record class UpdateTemplateCommandDto
{
        /// <summary>
        /// Gets the unique identifier of the template to update.
        /// </summary>
        public required Guid TemplateId { get; init; }

        /// <summary>
        /// Gets the updated display name for the template.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Gets the updated raw JSON schema payload.
        /// </summary>
        public required string SchemaJson { get; init; }

        /// <summary>
        /// Gets the expected concurrency stamp observed when the template was loaded.
        /// </summary>
        public required Guid ExpectedConcurrencyStamp { get; init; }

        /// <summary>
        /// Gets the trusted actor identifier for audit attribution.
        /// </summary>
        public required string ActorId { get; init; }
}

/// <summary>
/// Command DTO for archiving an existing exhibition template.
/// </summary>
public sealed record class DeleteTemplateCommandDto
{
        /// <summary>
        /// Gets the unique identifier of the template to archive.
        /// </summary>
        public required Guid TemplateId { get; init; }

        /// <summary>
        /// Gets the trusted actor identifier for audit attribution.
        /// </summary>
        public required string ActorId { get; init; }
}

/// <summary>
/// Query DTO for retrieving a paginated list of exhibition templates.
/// </summary>
public sealed record class ListTemplatesQueryDto
{
        /// <summary>
        /// Gets the requested 1-based page number.
        /// </summary>
        public int Page { get; init; } = FacadeConstants.DEFAULT_PAGE;

        /// <summary>
        /// Gets the requested number of items per page.
        /// </summary>
        public int PageSize { get; init; } = FacadeConstants.DEFAULT_PAGE_SIZE;

        /// <summary>
        /// Gets an optional filter restricting results to a specific <see cref="TemplateComponentTypeDto"/>.
        /// </summary>
        public TemplateComponentTypeDto? ComponentType { get; init; }

        /// <summary>
        /// Gets optional search text matched against template display names.
        /// </summary>
        public string? SearchTerm { get; init; }
}

/// <summary>
/// Facade DTO representing a stored exhibition template and its indexed image dependencies.
/// </summary>
public sealed record class TemplateMetadataDto
{
        /// <summary>
        /// Gets the unique identifier of the template.
        /// </summary>
        public required Guid Id { get; init; }

        /// <summary>
        /// Gets the URL-safe external identifier (<c>{node}_{guid}</c>) of the template.
        /// </summary>
        public required string ExternalId { get; init; }

        /// <summary>
        /// Gets the display name of the template.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Gets the Blazor UI component category targeted by this template.
        /// </summary>
        public required TemplateComponentTypeDto ComponentType { get; init; }

        /// <summary>
        /// Gets the raw JSON schema payload defining the template structure.
        /// </summary>
        public required string SchemaJson { get; init; }

        /// <summary>
        /// Gets the monotonically increasing schema version number.
        /// </summary>
        public required int SchemaVersion { get; init; }

        /// <summary>
        /// Gets a value indicating whether the template has been archived.
        /// </summary>
        public required bool IsArchived { get; init; }

        /// <summary>
        /// Gets the sorted list of <c>picture.{node}.{guid}</c> picture references embedded in this template.
        /// </summary>
        public required IReadOnlyList<string> ReferencedPictureReferences { get; init; }

        /// <summary>
        /// Gets the UTC timestamp when the template was created.
        /// </summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        /// Gets the UTC timestamp when the template was last updated.
        /// </summary>
        public required DateTimeOffset UpdatedAt { get; init; }

        /// <summary>
        /// Gets the current domain-managed optimistic concurrency stamp.
        /// </summary>
        public required Guid ConcurrencyStamp { get; init; }
}

