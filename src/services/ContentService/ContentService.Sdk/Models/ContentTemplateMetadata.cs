namespace ContentService.Sdk.Models;

/// <summary>
/// Immutable metadata returned by ContentService for an exhibition template and its indexed image references.
/// </summary>
public sealed record class ContentTemplateMetadata
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
        public required ContentTemplateComponentType ComponentType { get; init; }

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
        public IReadOnlyList<string> ReferencedPictureReferences { get; init; } = [];

        /// <summary>
        /// Gets the UTC timestamp when the template was created.
        /// </summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        /// Gets the UTC timestamp when the template was last updated.
        /// </summary>
        public required DateTimeOffset UpdatedAt { get; init; }

        /// <summary>
        /// Gets the domain-managed optimistic concurrency stamp for this template record.
        /// </summary>
        public required Guid ConcurrencyStamp { get; init; }

        /// <summary>
        /// Compares two <see cref="ContentTemplateMetadata"/> instances using sequence equality for <see cref="ReferencedPictureReferences"/>.
        /// </summary>
        /// <param name="other">The other metadata record to compare against.</param>
        /// <returns><see langword="true"/> when all fields and referenced picture sequences match; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ContentTemplateMetadata? other)
        {
                if (ReferenceEquals(objA: this, objB: other))
                {
                        return true;
                }

                if (other is null)
                {
                        return false;
                }

                return this.Id == other.Id
                        && string.Equals(a: this.ExternalId, b: other.ExternalId, comparisonType: StringComparison.Ordinal)
                        && string.Equals(a: this.Name, b: other.Name, comparisonType: StringComparison.Ordinal)
                        && this.ComponentType == other.ComponentType
                        && string.Equals(a: this.SchemaJson, b: other.SchemaJson, comparisonType: StringComparison.Ordinal)
                        && this.SchemaVersion == other.SchemaVersion
                        && this.IsArchived == other.IsArchived
                        && this.ReferencedPictureReferences.SequenceEqual(second: other.ReferencedPictureReferences, comparer: StringComparer.Ordinal)
                        && this.CreatedAt == other.CreatedAt
                        && this.UpdatedAt == other.UpdatedAt
                        && this.ConcurrencyStamp == other.ConcurrencyStamp;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
                HashCode hash = new();
                hash.Add(value: this.Id);
                hash.Add(value: this.ExternalId);
                hash.Add(value: this.Name);
                hash.Add(value: this.ComponentType);
                hash.Add(value: this.SchemaJson);
                hash.Add(value: this.SchemaVersion);
                hash.Add(value: this.IsArchived);
                foreach (string pictureRef in this.ReferencedPictureReferences)
                {
                        hash.Add(value: pictureRef);
                }

                hash.Add(value: this.CreatedAt);
                hash.Add(value: this.UpdatedAt);
                hash.Add(value: this.ConcurrencyStamp);
                return hash.ToHashCode();
        }
}

