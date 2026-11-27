namespace ContentService.Sdk.Models;

/// <summary>
/// Immutable response representing a stateless, checksum-verified offline publish package returned by ContentService.
/// </summary>
public sealed record class ContentPublishBundleResponse
{
        /// <summary>
        /// Gets the optional template identifier associated with this bundle.
        /// </summary>
        public Guid? TemplateId { get; init; }

        /// <summary>
        /// Gets the optional raw template JSON schema included in the bundle.
        /// </summary>
        public string? TemplateSchemaJson { get; init; }

        /// <summary>
        /// Gets the raw exhibition implementation JSON authored in EditorService.
        /// </summary>
        public required string ImplementationJson { get; init; }

        /// <summary>
        /// Gets the deduplicated list of verified image assets required by the bundle.
        /// </summary>
        public required IReadOnlyList<ContentBundleAssetResponse> Assets { get; init; }

        /// <summary>
        /// Gets the deterministic SHA-256 checksum covering schema, implementation, and asset digests.
        /// </summary>
        public required string BundleChecksum { get; init; }
}

