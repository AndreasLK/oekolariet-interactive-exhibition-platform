namespace ContentService.Facade.Dtos;

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

