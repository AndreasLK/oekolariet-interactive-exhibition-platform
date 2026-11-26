using ContentService.Domain.Enums;

namespace ContentService.Application.Models;

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

