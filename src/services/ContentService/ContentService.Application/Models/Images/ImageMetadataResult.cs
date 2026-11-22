namespace ContentService.Application.Models;

/// <summary>
/// Immutable metadata representation of a stored exhibition image.
/// </summary>
public sealed record class ImageMetadataResult
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
        /// Gets the URL-safe external asset identifier (<c>{node}_{guid}</c>) for HTTP API routes.
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
        /// Gets the optional accessibility alternative text, or <see langword="null"/> when none was provided.
        /// </summary>
        public string? AltText { get; init; }

        /// <summary>
        /// Gets the optional normalized recursive album path, or <see langword="null"/> when stored at root.
        /// </summary>
        public string? AlbumPath { get; init; }

        /// <summary>
        /// Gets the normalized read-only search tags associated with this image.
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
}

