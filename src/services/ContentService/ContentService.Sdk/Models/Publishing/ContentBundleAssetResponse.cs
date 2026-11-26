namespace ContentService.Sdk.Models;

/// <summary>
/// Response describing a verified image asset included in an offline kiosk publish package.
/// </summary>
public sealed record class ContentBundleAssetResponse
{
        /// <summary>
        /// Gets the unique identifier of the referenced image.
        /// </summary>
        public required Guid ImageId { get; init; }

        /// <summary>
        /// Gets the shard-aware picture reference (<c>picture.{node}.{guid}</c>) used in template and implementation JSON.
        /// </summary>
        public required string PictureReference { get; init; }

        /// <summary>
        /// Gets the URL-safe external identifier (<c>{node}_{guid}</c>) used to fetch the image binary.
        /// </summary>
        public required string ExternalId { get; init; }

        /// <summary>
        /// Gets the normalized MIME content type of the image asset.
        /// </summary>
        public required string ContentType { get; init; }

        /// <summary>
        /// Gets the byte length of the image payload.
        /// </summary>
        public required long SizeBytes { get; init; }

        /// <summary>
        /// Gets the lowercase 64-character hexadecimal SHA-256 digest of the image payload.
        /// </summary>
        public required string Sha256Hash { get; init; }
}

