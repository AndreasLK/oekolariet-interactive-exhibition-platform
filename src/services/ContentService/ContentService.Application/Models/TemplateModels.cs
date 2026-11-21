using ContentService.Application.Common;
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

/// <summary>
/// Command payload for updating an existing template's name and schema JSON with optimistic concurrency checking.
/// </summary>
/// <remarks>
/// <c>ComponentType</c> is intentionally omitted because <see cref="Domain.Entities.TemplateEntity"/> freezes it at creation to avoid breaking authored drafts.
/// </remarks>
public sealed record class UpdateTemplateCommand
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
        /// Gets the identifier of the actor modifying the template for audit logging.
        /// </summary>
        public required string ActorId { get; init; }
}

/// <summary>
/// Query criteria for retrieving a paginated list of exhibition templates.
/// </summary>
public sealed record class ListTemplatesQuery
{
        /// <summary>
        /// Gets the requested 1-based page number.
        /// </summary>
        public int Page { get; init; } = ApplicationConstants.DEFAULT_PAGE;

        /// <summary>
        /// Gets the requested number of items per page.
        /// </summary>
        public int PageSize { get; init; } = ApplicationConstants.DEFAULT_PAGE_SIZE;

        /// <summary>
        /// Gets an optional filter restricting results to a specific <see cref="TemplateComponentType"/>.
        /// </summary>
        public TemplateComponentType? ComponentType { get; init; }

        /// <summary>
        /// Gets optional search text matched against template display names.
        /// </summary>
        public string? SearchTerm { get; init; }
}

/// <summary>
/// Immutable application representation of a stored exhibition template and its indexed image dependencies.
/// </summary>
public sealed record class TemplateResult
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
        public required TemplateComponentType ComponentType { get; init; }

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

/// <summary>
/// Command payload for archiving an existing exhibition template.
/// </summary>
public sealed record class DeleteTemplateCommand
{
        /// <summary>
        /// Gets the unique identifier of the template to archive.
        /// </summary>
        public required Guid TemplateId { get; init; }

        /// <summary>
        /// Gets the identifier of the actor archiving the template for audit logging.
        /// </summary>
        public required string ActorId { get; init; }
}

