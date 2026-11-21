using System.Text.Json;
using ContentService.Application.Interfaces;
using ContentService.Domain.Entities;
using ContentService.Domain.Exceptions;
using ContentService.Domain.ValueObjects;

namespace ContentService.Application.Common;

/// <summary>
/// Centralizes JSON object validation and batch verification of referenced <see cref="PictureReference"/> assets.
/// </summary>
/// <remarks>
/// Extracted into a shared helper because template creation, template updates, and publish-bundle resolution enforce identical JSON and cross-shard image checks.
/// </remarks>
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
        /// Batch-loads referenced images and ensures every <see cref="PictureReference"/> exists on its expected storage node and is not soft-deleted.
        /// </summary>
        /// <param name="imageRepository">The image metadata repository used to resolve referenced IDs.</param>
        /// <param name="references">The deduplicated collection of picture references to verify.</param>
        /// <param name="cancellationToken">A token to observe while awaiting repository access.</param>
        /// <returns>The deduplicated list of verified active <see cref="ImageEntity"/> instances.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when one or more referenced images are missing, soft-deleted, or stored on a different shard.</exception>
        public static async Task<IReadOnlyList<ImageEntity>> VerifyAndResolveAsync(
                IImageRepository imageRepository,
                IReadOnlyCollection<PictureReference> references,
                CancellationToken cancellationToken)
        {
                // Skip unnecessary repository queries when a template or implementation references zero images.
                if (references.Count == 0)
                {
                        return [];
                }

                // Deduplicate image IDs across shards so a single batch query fetches all candidate records.
                HashSet<Guid> distinctIds = new(references.Count);
                foreach (PictureReference reference in references)
                {
                        distinctIds.Add(reference.ImageId);
                }

                IReadOnlyList<ImageEntity> fetchedImages = await imageRepository.GetByIdsAsync(
                        ids: distinctIds,
                        cancellationToken: cancellationToken);

                // Index active records by (StorageNodeId, ImageId) to verify both existence and shard placement.
                Dictionary<(string StorageNodeId, Guid ImageId), ImageEntity> activeImagesByShardKey = BuildActiveImageLookup(fetchedImages: fetchedImages);
                return MatchReferencesOrThrow(references: references, activeImagesByShardKey: activeImagesByShardKey);
        }

        /// <summary>
        /// Builds an active-image lookup keyed by composite <c>(StorageNodeId, ImageId)</c> shard identity.
        /// </summary>
        /// <param name="fetchedImages">The images returned from the repository batch query.</param>
        /// <returns>A dictionary containing only active (non-deleted) images.</returns>
        private static Dictionary<(string StorageNodeId, Guid ImageId), ImageEntity> BuildActiveImageLookup(
                IReadOnlyList<ImageEntity> fetchedImages)
        {
                Dictionary<(string StorageNodeId, Guid ImageId), ImageEntity> lookup = new(fetchedImages.Count);
                foreach (ImageEntity image in fetchedImages)
                {
                        // Exclude soft-deleted images so new templates and publish bundles cannot bind to deleted assets.
                        if (image.IsDeleted)
                        {
                                continue;
                        }

                        // Uses TryAdd instead of ToDictionary so duplicate rows from a repository query cannot crash with ArgumentException.
                        lookup.TryAdd(key: (image.StorageNodeId, image.Id), value: image);
                }

                return lookup;
        }

        /// <summary>
        /// Resolves every requested <see cref="PictureReference"/> against the active lookup or throws listing all missing references.
        /// </summary>
        /// <param name="references">The requested picture references.</param>
        /// <param name="activeImagesByShardKey">The lookup of active images indexed by shard and ID.</param>
        /// <returns>A deduplicated list of resolved <see cref="ImageEntity"/> objects.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when any picture reference cannot be matched to an active image on the expected shard.</exception>
        private static List<ImageEntity> MatchReferencesOrThrow(
                IReadOnlyCollection<PictureReference> references,
                Dictionary<(string StorageNodeId, Guid ImageId), ImageEntity> activeImagesByShardKey)
        {
                List<string> missingReferences = [];
                List<ImageEntity> resolvedImages = new(references.Count);
                HashSet<(string StorageNodeId, Guid ImageId)> seenKeys = new(references.Count);

                foreach (PictureReference reference in references)
                {
                        (string StorageNodeId, Guid ImageId) shardKey = (reference.StorageNodeId, reference.ImageId);
                        if (!activeImagesByShardKey.TryGetValue(key: shardKey, value: out ImageEntity? image))
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

