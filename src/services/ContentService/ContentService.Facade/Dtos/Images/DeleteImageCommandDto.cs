namespace ContentService.Facade.Dtos;

/// <summary>
/// Command DTO for soft-deleting an unreferenced exhibition image.
/// </summary>
public sealed record class DeleteImageCommandDto
{
        /// <summary>
        /// Gets the unique identifier of the image to soft-delete.
        /// </summary>
        public required Guid ImageId { get; init; }

        /// <summary>
        /// Gets the optional target storage node identifier when deleting via a shard-aware external ID.
        /// </summary>
        public string? StorageNodeId { get; init; }

        /// <summary>
        /// Gets a value indicating whether a bare-GUID delete should fan out to peer storage nodes when absent locally.
        /// </summary>
        public bool AllowPeerFallback { get; init; } = true;

        /// <summary>
        /// Gets the trusted actor identifier for audit attribution.
        /// </summary>
        public required string ActorId { get; init; }
}

