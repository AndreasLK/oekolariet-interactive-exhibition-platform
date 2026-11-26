namespace ContentService.Sdk.Models;

/// <summary>
/// Request payload for resolving and checksum-sealing an offline exhibition publish bundle.
/// </summary>
public sealed record class ResolvePublishBundleRequest
{
        /// <summary>
        /// Gets the optional template identifier whose schema and image references should be bundled alongside <see cref="ImplementationJson"/>.
        /// </summary>
        public Guid? TemplateId { get; init; }

        /// <summary>
        /// Gets the raw exhibition implementation JSON authored in EditorService.
        /// </summary>
        public required string ImplementationJson { get; init; }
}

/// <summary>
/// Describes a verified image asset required by a resolved offline kiosk publish bundle.
/// </summary>
public sealed record class ContentBundleAsset
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
        /// Gets the normalized lowercase MIME content type of the image asset.
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

/// <summary>
/// Represents a stateless, checksum-verified offline publish package returned by ContentService.
/// </summary>
public sealed record class ContentPublishBundle
{
        /// <summary>
        /// Gets the optional template identifier associated with this bundle.
        /// </summary>
        public Guid? TemplateId { get; init; }

        /// <summary>
        /// Gets the template's raw JSON schema if <see cref="TemplateId"/> was provided; otherwise <see langword="null"/>.
        /// </summary>
        public string? TemplateSchemaJson { get; init; }

        /// <summary>
        /// Gets the exhibition implementation JSON included in the bundle.
        /// </summary>
        public required string ImplementationJson { get; init; }

        /// <summary>
        /// Gets the deterministically ordered list of deduplicated image assets required by this bundle.
        /// </summary>
        public required IReadOnlyList<ContentBundleAsset> Assets { get; init; }

        /// <summary>
        /// Gets the deterministic SHA-256 checksum computed over the template schema, implementation JSON, and sorted asset digests.
        /// </summary>
        public required string BundleChecksum { get; init; }
}

