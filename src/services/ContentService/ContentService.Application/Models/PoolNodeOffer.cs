namespace ContentService.Application.Models;

/// <summary>
/// Represents an offer or location response from a healthy <c>ContentService</c> node in the stateless Dapr Pub/Sub pool.
/// </summary>
public sealed record class PoolNodeOffer
{
        /// <summary>
        /// Gets the responding node's <c>StorageNodeId</c>.
        /// </summary>
        public required string StorageNodeId { get; init; }

        /// <summary>
        /// Gets the responding node's Dapr application ID (e.g., <c>contentservice-node-2</c>).
        /// </summary>
        public required string DaprAppId { get; init; }

        /// <summary>
        /// Gets the optional public HTTP base URL advertised by the responding node (e.g., <c>http://localhost:5102</c>).
        /// </summary>
        public string? PublicBaseUrl { get; init; }

        /// <summary>
        /// Gets the remaining storage quota in bytes reported by the responding node.
        /// </summary>
        public long AvailableBytes { get; init; }
}

