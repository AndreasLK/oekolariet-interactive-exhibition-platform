namespace ContentService.Sdk.Models;

/// <summary>
/// Response representing a healthy storage node offer returned by the ContentService pool scatter-gather endpoint.
/// </summary>
public sealed record class ContentPoolNodeOfferResponse
{
        /// <summary>
        /// Gets the unique storage node identifier advertising available capacity.
        /// </summary>
        public required string StorageNodeId { get; init; }

        /// <summary>
        /// Gets the Dapr application identifier of the responding storage node.
        /// </summary>
        public required string DaprAppId { get; init; }

        /// <summary>
        /// Gets the optional public HTTP base URL advertised by the responding node.
        /// </summary>
        public string? PublicBaseUrl { get; init; }

        /// <summary>
        /// Gets the remaining available storage quota in bytes reported by the node.
        /// </summary>
        public required long AvailableBytes { get; init; }
}

