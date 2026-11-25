using System.Text.Json;
using ContentService.Application.Interfaces;
using ContentService.Application.Models;
using ContentService.Domain.Exceptions;
using ContentService.Domain.ValueObjects;

namespace ContentService.Application.Common;

/// <summary>
/// Centralizes JSON object validation and batch verification of referenced <see cref="PictureReference"/> assets across local and remote storage nodes.
/// </summary>
internal static class ReferencedImageVerifier
{
        /// <summary>
        /// Validates that a raw JSON payload is non-empty, within size limits, syntactically valid, and has a JSON object root.
        /// </summary>
        /// <param name="jsonPayload">The raw JSON string supplied by the caller.</param>
        /// <param name="maxLength">The maximum permitted UTF-16 character length.</param>
        /// <param name="fieldName">The human-readable field name included in validation error messages.</param>
        /// <exception cref="DomainValidationException">Thrown when the payload is blank, oversized, malformed JSON, or not a JSON object.</exception>
        public static void ValidateJsonObject(string? jsonPayload, int maxLength, string fieldName)
        {
                if (string.IsNullOrWhiteSpace(jsonPayload))
                {
                        throw new DomainValidationException($"{fieldName} cannot be null or whitespace.");
                }

                if (jsonPayload.Length > maxLength)
                {
                        throw new DomainValidationException($"{fieldName} cannot exceed {maxLength} characters.");
                }

                try
                {
                        // Dispose JsonDocument immediately because it rents pooled parsing buffers.
                        using JsonDocument document = JsonDocument.Parse(jsonPayload);

                        // Reject primitive JSON literals such as "null", "123", or strings that JsonDocument.Parse otherwise accepts.
                        if (document.RootElement.ValueKind != JsonValueKind.Object)
                        {
                                throw new DomainValidationException($"{fieldName} must be a JSON object.");
                        }
                }
                catch (JsonException exception)
                {
                        // Translate parser failures into DomainValidationException so malformed client JSON maps to 400 validation errors.
                        throw new DomainValidationException(
                                message: $"{fieldName} is not valid JSON.",
                                innerException: exception);
                }
        }

        /// <summary>
        /// Batch-loads referenced images from the local repository and any remote storage nodes, ensuring every <see cref="PictureReference"/> exists on its expected node and is active.
        /// </summary>
        /// <param name="imageRepository">The local image metadata repository.</param>
        /// <param name="references">The deduplicated collection of picture references to verify.</param>
        /// <param name="cancellationToken">A token to observe while awaiting repository or peer-node access.</param>
        /// <param name="storageNodeClient">Optional peer storage node client for resolving images hosted on other ContentService instances.</param>
        /// <param name="localStorageNodeId">Optional local storage node identifier; when omitted, all lookups run against <paramref name="imageRepository"/>.</param>
        /// <returns>The deduplicated list of verified active <see cref="ImageMetadataResult"/> projections.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when one or more referenced images are missing, soft-deleted, or stored on a different shard.</exception>
        public static async Task<IReadOnlyList<ImageMetadataResult>> VerifyAndResolveAsync(
                IImageRepository imageRepository,
                IReadOnlyCollection<PictureReference> references,
                CancellationToken cancellationToken,
                IStorageNodeClient? storageNodeClient = null,
                string? localStorageNodeId = null)
        {
                // Skip unnecessary repository/Dapr queries when a template or implementation references zero images.
                if (references.Count == 0)
                {
                        return [];
                }

                Dictionary<(string StorageNodeId, Guid ImageId), ImageMetadataResult> activeImagesByShardKey =
                        await ReferencedImageNodeFetcher.FetchAllAsync(
                                imageRepository: imageRepository,
                                references: references,
                                storageNodeClient: storageNodeClient,
                                localStorageNodeId: localStorageNodeId,
                                cancellationToken: cancellationToken);

                return MatchReferencesOrThrow(
                        references: references,
                        activeImagesByShardKey: activeImagesByShardKey);
        }

        /// <summary>
        /// Resolves every requested <see cref="PictureReference"/> against the active lookup or throws listing all missing references.
        /// </summary>
        private static List<ImageMetadataResult> MatchReferencesOrThrow(
                IReadOnlyCollection<PictureReference> references,
                Dictionary<(string StorageNodeId, Guid ImageId), ImageMetadataResult> activeImagesByShardKey)
        {
                List<string> missingReferences = [];
                List<ImageMetadataResult> resolvedImages = new(references.Count);
                HashSet<(string StorageNodeId, Guid ImageId)> seenKeys = new(references.Count);

                foreach (PictureReference reference in references)
                {
                        (string StorageNodeId, Guid ImageId) shardKey = (reference.StorageNodeId, reference.ImageId);
                        if (!activeImagesByShardKey.TryGetValue(key: shardKey, value: out ImageMetadataResult? image))
                        {
                                // Collect all missing, deleted, or cross-shard references before throwing so callers can fix every broken reference in one pass.
                                missingReferences.Add(reference.ToString());
                                continue;
                        }

                        // Emit each verified image once even when multiple JSON fields reference the same picture.
                        if (seenKeys.Add(shardKey))
                        {
                                resolvedImages.Add(image);
                        }
                }

                if (missingReferences.Count > 0)
                {
                        // Sort missing references deterministically so error messages remain reproducible across runs.
                        missingReferences.Sort(StringComparer.Ordinal);
                        throw new KeyNotFoundException(
                                $"Referenced image(s) could not be found: {string.Join(separator: ", ", values: missingReferences)}.");
                }

                return resolvedImages;
        }
}
