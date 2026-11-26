namespace ContentService.Sdk.Models;

/// <summary>
/// Immutable response returned by ContentService for an active uploaded exhibition image.
/// </summary>
public sealed record class ContentImageResponse
{
        /// <summary>
        /// Gets the unique identifier of the image.
        /// </summary>
        public required Guid Id { get; init; }

        /// <summary>
        /// Gets the picture reference (<c>picture.{node}.{guid}</c>) used inside template JSON schemas.
        /// </summary>
        public required string PictureReference { get; init; }

        /// <summary>
        /// Gets the URL-safe external asset identifier (<c>{node}_{guid}</c>) for HTTP routes.
        /// </summary>
        public required string ExternalId { get; init; }

        /// <summary>
        /// Gets the sanitized original file name of the stored image.
        /// </summary>
        public required string FileName { get; init; }

        /// <summary>
        /// Gets the normalized lowercase MIME content type.
        /// </summary>
        public required string ContentType { get; init; }

        /// <summary>
        /// Gets the verified payload size in bytes.
        /// </summary>
        public required long SizeBytes { get; init; }

        /// <summary>
        /// Gets the lowercase 64-character hexadecimal SHA-256 digest of the image payload.
        /// </summary>
        public required string Sha256Hash { get; init; }

        /// <summary>
        /// Gets the optional accessibility alternative text / description, or <see langword="null"/> when none was provided.
        /// </summary>
        public string? AltText { get; init; }

        /// <summary>
        /// Gets the optional normalized recursive album path, or <see langword="null"/> when stored at root.
        /// </summary>
        public string? AlbumPath { get; init; }

        /// <summary>
        /// Gets the normalized search tags associated with this image.
        /// </summary>
        public IReadOnlyList<string> Tags { get; init; } = [];

        /// <summary>
        /// Gets the UTC timestamp when the image was uploaded.
        /// </summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        /// Gets the domain-managed optimistic edit-lock stamp for this image record.
        /// </summary>
        public required Guid ConcurrencyStamp { get; init; }

        /// <summary>
        /// Compares two <see cref="ContentImageResponse"/> instances using sequence equality for <see cref="Tags"/>.
        /// </summary>
        /// <param name="other">The other image response record to compare against.</param>
        /// <returns><see langword="true"/> when all fields and tag sequences match; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ContentImageResponse? other)
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
                        && string.Equals(a: this.PictureReference, b: other.PictureReference, comparisonType: StringComparison.Ordinal)
                        && string.Equals(a: this.ExternalId, b: other.ExternalId, comparisonType: StringComparison.Ordinal)
                        && string.Equals(a: this.FileName, b: other.FileName, comparisonType: StringComparison.Ordinal)
                        && string.Equals(a: this.ContentType, b: other.ContentType, comparisonType: StringComparison.Ordinal)
                        && this.SizeBytes == other.SizeBytes
                        && string.Equals(a: this.Sha256Hash, b: other.Sha256Hash, comparisonType: StringComparison.Ordinal)
                        && string.Equals(a: this.AltText, b: other.AltText, comparisonType: StringComparison.Ordinal)
                        && string.Equals(a: this.AlbumPath, b: other.AlbumPath, comparisonType: StringComparison.Ordinal)
                        && this.Tags.SequenceEqual(second: other.Tags, comparer: StringComparer.Ordinal)
                        && this.CreatedAt == other.CreatedAt
                        && this.ConcurrencyStamp == other.ConcurrencyStamp;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
                HashCode hash = new();
                hash.Add(value: this.Id);
                hash.Add(value: this.PictureReference);
                hash.Add(value: this.ExternalId);
                hash.Add(value: this.FileName);
                hash.Add(value: this.ContentType);
                hash.Add(value: this.SizeBytes);
                hash.Add(value: this.Sha256Hash);
                hash.Add(value: this.AltText);
                hash.Add(value: this.AlbumPath);
                foreach (string tag in this.Tags)
                {
                        hash.Add(value: tag);
                }

                hash.Add(value: this.CreatedAt);
                hash.Add(value: this.ConcurrencyStamp);
                return hash.ToHashCode();
        }
}

