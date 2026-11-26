namespace ContentService.Facade.Dtos;

/// <summary>
/// Query DTO for resolving and checksum-sealing a self-contained offline publish bundle.
/// </summary>
public sealed record class ResolvePublishBundleQueryDto
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
/// Facade DTO describing a verified image asset included in an offline kiosk publish package.
/// </summary>
public sealed record class BundleAssetDto
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

/// <summary>
/// Facade DTO representing a stateless, checksum-verified offline publish package.
/// </summary>
public sealed record class PublishBundleDto
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
        public required IReadOnlyList<BundleAssetDto> Assets { get; init; }

        /// <summary>
        /// Gets the deterministic SHA-256 checksum computed over the template schema, implementation JSON, and sorted asset digests.
        /// </summary>
        public required string BundleChecksum { get; init; }
}

/// <summary>
/// Represents the disk capacity health state of a ContentService storage node at the facade boundary.
/// </summary>
public enum StorageHealthStatusDto
{
        /// <summary>
        /// Storage usage is below the configured warning threshold and accepts normal read and write operations.
        /// </summary>
        Healthy = 0,

        /// <summary>
        /// Storage usage has reached the warning threshold, signaling operators to provision additional capacity.
        /// </summary>
        Warning = 1,

        /// <summary>
        /// Storage capacity is exhausted; the node rejects new uploads while continuing to serve existing assets.
        /// </summary>
        FullReadOnly = 2
}

/// <summary>
/// Facade DTO reporting current disk quota consumption and health status for a ContentService storage node.
/// </summary>
public sealed record class StorageHealthDto
{
        /// <summary>
        /// Gets the evaluated storage health status.
        /// </summary>
        public required StorageHealthStatusDto Status { get; init; }

        /// <summary>
        /// Gets the non-negative byte usage currently recorded on the storage node.
        /// </summary>
        public required long UsedBytes { get; init; }

        /// <summary>
        /// Gets the configured maximum byte quota for the storage node.
        /// </summary>
        public required long QuotaBytes { get; init; }

        /// <summary>
        /// Gets the computed percentage of quota currently consumed.
        /// </summary>
        public required double UtilizationPercent { get; init; }
}

