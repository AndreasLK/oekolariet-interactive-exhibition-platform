namespace ContentService.Facade.Dtos;

/// <summary>
/// Facade DTO representing persisted metadata for an active exhibition image.
/// </summary>
public sealed record class ImageMetadataDto
{
        /// <summary>
        /// Gets the unique identifier of the image.
        /// </summary>
        public required Guid Id { get; init; }

        /// <summary>
        /// Gets the shard-aware picture reference (<c>picture.{node}.{guid}</c>) used inside template schemas.
        /// </summary>
        public required string PictureReference { get; init; }

        /// <summary>
        /// Gets the URL-safe external identifier (<c>{node}_{guid}</c>) for HTTP routes.
        /// </summary>
        public required string ExternalId { get; init; }

        /// <summary>
        /// Gets the sanitized original file name.
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
        /// Gets the optional accessibility alternative text.
        /// </summary>
        public string? AltText { get; init; }

        /// <summary>
        /// Gets the optional normalized recursive album path.
        /// </summary>
        public string? AlbumPath { get; init; }

        /// <summary>
        /// Gets the normalized search tags associated with this image.
        /// </summary>
        public required IReadOnlyList<string> Tags { get; init; }

        /// <summary>
        /// Gets the UTC timestamp when the image was uploaded.
        /// </summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        /// Gets the current domain-managed optimistic concurrency stamp.
        /// </summary>
        public required Guid ConcurrencyStamp { get; init; }
}

