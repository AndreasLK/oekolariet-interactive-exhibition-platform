using ContentService.Application.Interfaces;
using ContentService.Application.Models;
using ContentService.Domain.Entities;
using ContentService.Domain.ValueObjects;

namespace ContentService.Application.Common;

/// <summary>
/// Batches referenced image metadata lookups across the local SQL repository and remote peer storage nodes.
/// </summary>
internal static class ReferencedImageNodeFetcher
{
        /// <summary>
        /// Partitions references into local vs. remote node batches so each peer node is queried at most once instead of issuing N+1 per-image requests.
        /// </summary>
        /// <param name="imageRepository">The local image metadata repository.</param>
        /// <param name="references">The deduplicated collection of picture references to fetch.</param>
        /// <param name="storageNodeClient">Optional peer storage node client.</param>
        /// <param name="localStorageNodeId">Optional local storage node identifier.</param>
        /// <param name="cancellationToken">Propagates cancellation.</param>
        /// <returns>A lookup of active <see cref="ImageMetadataResult"/> projections indexed by <c>(StorageNodeId, ImageId)</c>.</returns>
        public static async Task<Dictionary<(string StorageNodeId, Guid ImageId), ImageMetadataResult>> FetchAllAsync(
                IImageRepository imageRepository,
                IReadOnlyCollection<PictureReference> references,
                IStorageNodeClient? storageNodeClient,
                string? localStorageNodeId,
                CancellationToken cancellationToken)
        {
                HashSet<Guid> localIds = [];
                Dictionary<string, HashSet<Guid>> remoteIdsByNode = new(StringComparer.Ordinal);

                foreach (PictureReference reference in references)
                {
                        bool isLocalReference = string.IsNullOrWhiteSpace(localStorageNodeId)
                                || storageNodeClient is null
                                || string.Equals(
                                        a: reference.StorageNodeId,
                                        b: localStorageNodeId.Trim(),
                                        comparisonType: StringComparison.OrdinalIgnoreCase);

                        if (isLocalReference)
                        {
                                localIds.Add(reference.ImageId);
                                continue;
                        }

                        if (!remoteIdsByNode.TryGetValue(key: reference.StorageNodeId, value: out HashSet<Guid>? nodeIds))
                        {
                                nodeIds = [];
                                remoteIdsByNode[reference.StorageNodeId] = nodeIds;
                        }

                        nodeIds.Add(reference.ImageId);
                }

                Dictionary<(string StorageNodeId, Guid ImageId), ImageMetadataResult> lookup = new(references.Count);

                await PopulateLocalLookupAsync(
                        imageRepository: imageRepository,
                        localIds: localIds,
                        lookup: lookup,
                        cancellationToken: cancellationToken);

                await PopulateRemoteLookupsAsync(
                        storageNodeClient: storageNodeClient,
                        remoteIdsByNode: remoteIdsByNode,
                        lookup: lookup,
                        cancellationToken: cancellationToken);

                return lookup;
        }

        /// <summary>
        /// Queries the local SQL repository for local-node image IDs and indexes active projections by composite shard key.
        /// </summary>
        private static async Task PopulateLocalLookupAsync(
                IImageRepository imageRepository,
                HashSet<Guid> localIds,
                Dictionary<(string StorageNodeId, Guid ImageId), ImageMetadataResult> lookup,
                CancellationToken cancellationToken)
        {
                if (localIds.Count == 0)
                {
                        return;
                }

                IReadOnlyList<ImageEntity> fetchedImages = await imageRepository.GetByIdsAsync(
                        ids: localIds,
                        cancellationToken: cancellationToken);

                foreach (ImageEntity image in fetchedImages)
                {
                        // Exclude soft-deleted images so new templates and publish bundles cannot bind to deleted assets.
                        if (image.IsDeleted)
                        {
                                continue;
                        }

                        // Uses TryAdd instead of ToDictionary so duplicate rows from a repository query cannot crash with ArgumentException.
                        lookup.TryAdd(
                                key: (image.StorageNodeId, image.Id),
                                value: ContentResultMapper.ToMetadataResult(image: image));
                }
        }

        /// <summary>
        /// Queries each remote storage node once via <see cref="IStorageNodeClient"/> and indexes returned metadata by shard key.
        /// </summary>
        private static async Task PopulateRemoteLookupsAsync(
                IStorageNodeClient? storageNodeClient,
                Dictionary<string, HashSet<Guid>> remoteIdsByNode,
                Dictionary<(string StorageNodeId, Guid ImageId), ImageMetadataResult> lookup,
                CancellationToken cancellationToken)
        {
                if (storageNodeClient is null || remoteIdsByNode.Count == 0)
                {
                        return;
                }

                foreach (KeyValuePair<string, HashSet<Guid>> nodeGroup in remoteIdsByNode)
                {
                        IReadOnlyList<ImageMetadataResult> remoteImages = await storageNodeClient.GetImagesMetadataBatchAsync(
                                storageNodeId: nodeGroup.Key,
                                imageIds: nodeGroup.Value,
                                cancellationToken: cancellationToken);

                        foreach (ImageMetadataResult remoteImage in remoteImages)
                        {
                                // WHY: Verify the returned ExternalId actually belongs to the expected node so a misrouted node cannot spoof shard ownership.
                                if (PictureReference.TryParseExternalId(
                                        externalId: remoteImage.ExternalId,
                                        storageNodeId: out string actualNodeId,
                                        imageId: out Guid actualImageId))
                                {
                                        lookup.TryAdd(key: (actualNodeId, actualImageId), value: remoteImage);
                                }
                        }
                }
        }
}

