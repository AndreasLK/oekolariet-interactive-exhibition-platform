namespace ContentService.Facade.Dtos;

/// <summary>
/// Command DTO for updating an active image's searchable metadata with optimistic concurrency checking.
/// </summary>
public sealed record class UpdateImageMetadataCommandDto
{
        /// <summary>
        /// Gets the unique identifier of the image to update.
        /// </summary>
        public required Guid ImageId { get; init; }

        /// <summary>
        /// Gets the optional target storage node identifier when updating via a shard-aware external ID.
        /// </summary>
        public string? StorageNodeId { get; init; }

        /// <summary>
        /// Gets a value indicating whether a bare-GUID update should fan out to peer storage nodes when absent locally.
        /// </summary>
        public bool AllowPeerFallback { get; init; } = true;

        /// <summary>
        /// Gets the updated accessibility alternative text, or <see langword="null"/> to clear it.
        /// </summary>
        public string? AltText { get; init; }

        /// <summary>
        /// Gets the updated comma-separated search tags, or <see langword="null"/> to clear them.
        /// </summary>
        public string? Tags { get; init; }

        /// <summary>
        /// Gets the updated recursive album path, or <see langword="null"/> for root.
        /// </summary>
        public string? AlbumPath { get; init; }

        /// <summary>
        /// Gets the expected concurrency stamp observed when the image was loaded.
        /// </summary>
        public required Guid ExpectedConcurrencyStamp { get; init; }

        /// <summary>
        /// Gets the trusted actor identifier for audit attribution.
        /// </summary>
        public required string ActorId { get; init; }
}

