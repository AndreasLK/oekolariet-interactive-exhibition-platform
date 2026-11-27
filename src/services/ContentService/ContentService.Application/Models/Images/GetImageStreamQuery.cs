namespace ContentService.Application.Models;

/// <summary>
/// Query criteria for loading an exhibition image's readable byte stream.
/// </summary>
public sealed record class GetImageStreamQuery
{
        /// <summary>
        /// Gets the unique identifier of the requested image.
        /// </summary>
        public required Guid ImageId { get; init; }

        /// <summary>
        /// Gets the optional target storage node identifier when resolving a shard-aware external ID.
        /// </summary>
        public string? StorageNodeId { get; init; }

        /// <summary>
        /// Gets a value indicating whether a bare-GUID lookup should fan out to peer storage nodes when absent locally.
        /// </summary>
        public bool AllowPeerFallback { get; init; } = true;
}

