using ContentService.Domain.Enums;

namespace ContentService.Application.Models;

/// <summary>
/// Command payload for resolving and checksum-sealing a self-contained offline publish bundle for kiosk servers.
/// </summary>
public sealed record class ResolvePublishBundleCommand
{
        /// <summary>
        /// Gets the optional template identifier whose schema and image references should be bundled alongside <see cref="ImplementationJson"/>.
        /// </summary>
        /// <remarks>
        /// Nullable because bespoke one-off exhibition implementations can publish directly with custom layout JSON without referencing a saved template.
        /// </remarks>
        public Guid? TemplateId { get; init; }

        /// <summary>
        /// Gets the raw exhibition implementation JSON authored in UI Editor.
        /// </summary>
        public required string ImplementationJson { get; init; }
}

/// <summary>
/// Describes a single verified image asset included in an offline kiosk publish package.
/// </summary>
public sealed record class BundleAssetResult
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
/// Represents a stateless, checksum-verified offline publish package delivered to kiosk servers.
/// </summary>
public sealed record class PublishBundleResult
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
        public required IReadOnlyList<BundleAssetResult> Assets { get; init; }

        /// <summary>
        /// Gets the deterministic SHA-256 checksum computed over the template schema, implementation JSON, and sorted asset digests.
        /// </summary>
        public required string BundleChecksum { get; init; }
}

/// <summary>
/// Reports current disk quota consumption and health status for a ContentService storage node.
/// </summary>
public sealed record class StorageHealthResult
{
        /// <summary>
        /// Gets the evaluated storage health status (<see cref="StorageHealthStatus"/>).
        /// </summary>
        public required StorageHealthStatus Status { get; init; }

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

