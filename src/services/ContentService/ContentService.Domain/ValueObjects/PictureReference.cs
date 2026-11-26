using ContentService.Domain.Common;
using ContentService.Domain.Exceptions;

namespace ContentService.Domain.ValueObjects;

/// <summary>
/// Represents a shard-aware image reference pairing a storage node identifier with an image GUID.
/// </summary>
/// <remarks>
/// Non-standard identifier design: Encodes the owning <see cref="StorageNodeId"/> directly inside the handle
/// (<c>picture.{node}.{guid}</c> / <c>{node}_{guid}</c>) instead of using opaque bare GUIDs.
/// Gain: Zero-lookup shard routing to the owning storage node and single-segment HTTP URLs.
/// Loss: Handles are tied to their originating node ID unless an alias/redirect layer is introduced later.
/// </remarks>
// Implemented as a sealed record class instead of a struct so default(PictureReference) cannot exist in an invalid unvalidated state.
public sealed record class PictureReference
{
        /// <summary>
        /// Initializes a new <see cref="PictureReference"/> from a storage node identifier and image GUID.
        /// </summary>
        /// <param name="storageNodeId">The storage node (shard) identifier owning the image.</param>
        /// <param name="imageId">The unique identifier of the image within that node.</param>
        /// <exception cref="DomainValidationException">Thrown when <paramref name="storageNodeId"/> is invalid or <paramref name="imageId"/> is <see cref="Guid.Empty"/>.</exception>
        public PictureReference(string storageNodeId, Guid imageId)
        {
                string validatedStorageNodeId = ValidateStorageNodeId(storageNodeId: storageNodeId);
                if (imageId == Guid.Empty)
                {
                        throw new DomainValidationException("Image ID cannot be empty.");
                }

                this.StorageNodeId = validatedStorageNodeId;
                this.ImageId = imageId;
        }

        /// <summary>
        /// Gets the storage node (shard) identifier where the image is stored.
        /// </summary>
        public string StorageNodeId { get; }

        /// <summary>
        /// Gets the unique identifier of the referenced image.
        /// </summary>
        public Guid ImageId { get; }

        /// <summary>
        /// Formats this reference as a template schema handle (<c>picture.{node}.{guid}</c>).
        /// </summary>
        /// <returns>The formatted picture reference handle.</returns>
        public override string ToString()
        {
                // Embedding the storage node ID lets the Content Gateway route requests straight to the right shard without a database lookup.
                return $"{DomainConstants.PICTURE_REFERENCE_PREFIX}{this.StorageNodeId}{DomainConstants.PICTURE_REFERENCE_SEGMENT_SEPARATOR}{this.ImageId.ToString(DomainConstants.GUID_FORMAT_HYPHENATED)}";
        }

        /// <summary>
        /// Formats this reference as an external asset identifier (<c>{node}_{guid}</c>) for API routes and URLs.
        /// </summary>
        /// <returns>The external asset identifier string.</returns>
        public string ToExternalImageId()
        {
                // Uses '_' instead of '.' so external HTTP URLs keep a single clean path segment.
                return $"{this.StorageNodeId}{DomainConstants.EXTERNAL_IMAGE_ID_SEPARATOR}{this.ImageId.ToString(DomainConstants.GUID_FORMAT_HYPHENATED)}";
        }

        /// <summary>
        /// Parses a template picture handle (<c>picture.{node}.{guid}</c>) into a <see cref="PictureReference"/>.
        /// </summary>
        /// <param name="rawValue">The raw handle string to parse.</param>
        /// <returns>The parsed <see cref="PictureReference"/>.</returns>
        public static PictureReference Parse(string? rawValue) => PictureReferenceParser.Parse(rawValue: rawValue);

        /// <summary>
        /// Attempts to parse a template picture handle (<c>picture.{node}.{guid}</c>) without throwing on invalid input.
        /// </summary>
        /// <param name="rawValue">The raw handle string to parse.</param>
        /// <param name="reference">When this method returns <see langword="true"/>, contains the parsed reference; otherwise <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if <paramref name="rawValue"/> is a valid picture reference; otherwise <see langword="false"/>.</returns>
        public static bool TryParse(string? rawValue, out PictureReference? reference) =>
                PictureReferenceParser.TryParse(rawValue: rawValue, reference: out reference);

        /// <summary>
        /// Parses an external asset identifier (<c>{node}_{guid}</c>) into its storage node identifier and image GUID.
        /// </summary>
        /// <param name="externalId">The external asset identifier to parse.</param>
        /// <returns>A tuple containing the validated storage node identifier and image GUID.</returns>
        public static (string StorageNodeId, Guid ImageId) ParseExternalId(string? externalId) =>
                PictureReferenceParser.ParseExternalId(externalId: externalId);

        /// <summary>
        /// Attempts to parse an external asset identifier (<c>{node}_{guid}</c>) without throwing on invalid input.
        /// </summary>
        /// <param name="externalId">The external asset identifier to parse.</param>
        /// <param name="storageNodeId">When this method returns <see langword="true"/>, contains the validated storage node identifier.</param>
        /// <param name="imageId">When this method returns <see langword="true"/>, contains the parsed non-empty image GUID.</param>
        /// <returns><see langword="true"/> if <paramref name="externalId"/> is valid; otherwise <see langword="false"/>.</returns>
        public static bool TryParseExternalId(string? externalId, out string storageNodeId, out Guid imageId) =>
                PictureReferenceParser.TryParseExternalId(
                        externalId: externalId,
                        storageNodeId: out storageNodeId,
                        imageId: out imageId);

        /// <summary>
        /// Scans a template JSON payload for unique managed picture handles while rejecting inline base64 image data URIs.
        /// </summary>
        /// <param name="jsonPayload">The raw template JSON payload to scan.</param>
        /// <returns>A deduplicated list of <see cref="PictureReference"/> values discovered in <paramref name="jsonPayload"/>.</returns>
        public static List<PictureReference> ExtractReferences(string? jsonPayload) =>
                TemplateJsonPictureScanner.ExtractReferences(jsonPayload: jsonPayload);

        /// <summary>
        /// Validates and trims a storage node identifier according to shard routing-key rules.
        /// </summary>
        /// <param name="storageNodeId">The raw storage node identifier to validate.</param>
        /// <returns>The trimmed, valid storage node identifier.</returns>
        internal static string ValidateStorageNodeId(string? storageNodeId) =>
                PictureReferenceParser.ValidateStorageNodeId(storageNodeId: storageNodeId);
}
