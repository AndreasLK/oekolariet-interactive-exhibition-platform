using ContentService.Application.Interfaces;
using ContentService.Application.Models;
using ContentService.Application.Options;

namespace ContentService.Application.Common;

/// <summary>
/// Centralizes storage-node comparison and peer-node fallback so all image use cases decide local vs. remote Dapr routing identically.
/// </summary>
internal static class StorageNodeTargetResolver
{
        /// <summary>
        /// Determines whether <paramref name="targetStorageNodeId"/> refers to a different storage node than <paramref name="localStorageNodeId"/>.
        /// </summary>
        /// <param name="targetStorageNodeId">The requested storage node identifier from the caller or external ID, or <see langword="null"/> for local.</param>
        /// <param name="localStorageNodeId">The configured local storage node identifier.</param>
        /// <returns><see langword="true"/> when the request must be routed to a peer node; otherwise <see langword="false"/>.</returns>
        public static bool IsRemoteNode(string? targetStorageNodeId, string? localStorageNodeId)
        {
                if (string.IsNullOrWhiteSpace(targetStorageNodeId)
                        || string.IsNullOrWhiteSpace(localStorageNodeId))
                {
                        return false;
                }

                // WHY: Normalize case and outer whitespace so config casing differences never cause a node to Dapr-invoke itself.
                return !string.Equals(
                        a: targetStorageNodeId.Trim(),
                        b: localStorageNodeId.Trim(),
                        comparisonType: StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Queries configured peer storage nodes sequentially when a bare-GUID image is not present on the local node.
        /// </summary>
        /// <typeparam name="TResult">The result type returned by the peer operation.</typeparam>
        /// <param name="options">The local storage options containing <see cref="ContentStorageOptions.PeerStorageNodeIds"/>.</param>
        /// <param name="allowPeerFallback"><see langword="false"/> for already-forwarded peer requests so fan-out never loops.</param>
        /// <param name="probePeerAsync">Delegate invoking a single peer node and returning <see langword="null"/> when not found there.</param>
        /// <param name="cancellationToken">Propagates cancellation across peer calls.</param>
        /// <returns>The first non-null peer result, or <see langword="null"/> if no peer owns the image.</returns>
        public static async Task<TResult?> TryFindInPeersAsync<TResult>(
                ContentStorageOptions? options,
                bool allowPeerFallback,
                Func<string, CancellationToken, Task<TResult?>> probePeerAsync,
                CancellationToken cancellationToken)
                where TResult : class
        {
                if (!allowPeerFallback || options?.PeerStorageNodeIds is null)
                {
                        return null;
                }

                foreach (string peerNodeId in options.PeerStorageNodeIds)
                {
                        if (!IsRemoteNode(targetStorageNodeId: peerNodeId, localStorageNodeId: options.StorageNodeId))
                        {
                                continue;
                        }

                        TResult? result = await probePeerAsync(peerNodeId, cancellationToken);
                        if (result is not null)
                        {
                                return result;
                        }
                }

                return null;
        }

        /// <summary>
        /// Locates a bare-GUID image via the stateless Dapr Pub/Sub pool first, falling back to static peer IDs when configured.
        /// </summary>
        /// <typeparam name="TResult">The result type returned by the owning node.</typeparam>
        /// <param name="imageId">The bare image identifier to locate.</param>
        /// <param name="options">The local storage options.</param>
        /// <param name="allowPeerFallback"><see langword="false"/> for already-forwarded peer requests.</param>
        /// <param name="poolCoordinator">Optional stateless Dapr Pub/Sub pool coordinator.</param>
        /// <param name="fetchFromNodeAsync">Delegate fetching the result from the located node.</param>
        /// <param name="cancellationToken">Propagates cancellation.</param>
        /// <returns>The resolved result from the owning peer, or <see langword="null"/> if not found.</returns>
        public static async Task<TResult?> ResolveAcrossPoolAsync<TResult>(
                Guid imageId,
                ContentStorageOptions? options,
                bool allowPeerFallback,
                IPoolCoordinator? poolCoordinator,
                Func<string, CancellationToken, Task<TResult?>> fetchFromNodeAsync,
                CancellationToken cancellationToken)
                where TResult : class
        {
                if (!allowPeerFallback)
                {
                        return null;
                }

                // WHY: Prefer stateless Dapr Pub/Sub scatter-gather so nodes do not need to know about all peers in advance.
                if (poolCoordinator is not null)
                {
                        PoolNodeOffer? offer = await poolCoordinator.LocateImageOwnerAsync(
                                imageId: imageId,
                                cancellationToken: cancellationToken);

                        if (offer is not null)
                        {
                                return await fetchFromNodeAsync(offer.StorageNodeId, cancellationToken);
                        }
                }

                return await TryFindInPeersAsync(
                        options: options,
                        allowPeerFallback: allowPeerFallback,
                        probePeerAsync: fetchFromNodeAsync,
                        cancellationToken: cancellationToken);
        }
}

