using System.Text.RegularExpressions;
using ContentService.Domain.Common;
using ContentService.Domain.Exceptions;

namespace ContentService.Domain.ValueObjects;

/// <summary>
/// Represents a shard-aware image reference pairing a storage node identifier with an image GUID.
/// </summary>
// Implemented as a sealed record class instead of a struct so default(PictureReference) cannot exist in an invalid unvalidated state.
public sealed record class PictureReference
{
        /// <summary>
        /// Expected number of parts when splitting a <c>{node}{separator}{guid}</c> payload.
        /// </summary>
        private const int EXPECTED_SEGMENT_COUNT = 2;

        /// <summary>
        /// Regex matching a standalone storage node identifier from start to end.
        /// </summary>
        private static readonly Regex NODE_ID_REGEX = BuildStandaloneNodeIdRegex();

        /// <summary>
        /// Regex locating <c>picture.{node}.{guid}</c> handles inside template JSON payloads.
        /// </summary>
        private static readonly Regex PICTURE_HANDLE_REGEX = BuildPictureHandleRegex();

        /// <summary>
        /// Initializes a new <see cref="PictureReference"/> from a storage node identifier and image GUID.
        /// </summary>
        /// <param name="storageNodeId">The storage node (shard) identifier owning the image.</param>
        /// <param name="imageId">The unique identifier of the image within that node.</param>
        /// <exception cref="DomainValidationException">Thrown when <paramref name="storageNodeId"/> is invalid or <paramref name="imageId"/> is <see cref="Guid.Empty"/>.</exception>
        public PictureReference(string storageNodeId, Guid imageId)
        {
                string validatedStorageNodeId = ValidateStorageNodeId(storageNodeId);
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
        /// <exception cref="DomainValidationException">Thrown when <paramref name="rawValue"/> is null or does not match the required handle format.</exception>
        public static PictureReference Parse(string? rawValue)
        {
                if (!TryParse(rawValue, out PictureReference? reference))
                {
                        throw new DomainValidationException($"Invalid picture reference format: '{rawValue}'.");
                }

                return reference!;
        }

        /// <summary>
        /// Attempts to parse a template picture handle (<c>picture.{node}.{guid}</c>) without throwing on invalid input.
        /// </summary>
        /// <param name="rawValue">The raw handle string to parse.</param>
        /// <param name="reference">When this method returns <see langword="true"/>, contains the parsed reference; otherwise <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if <paramref name="rawValue"/> is a valid picture reference; otherwise <see langword="false"/>.</returns>
        public static bool TryParse(string? rawValue, out PictureReference? reference)
        {
                reference = null;
                if (string.IsNullOrEmpty(rawValue)
                        || !rawValue.StartsWith(DomainConstants.PICTURE_REFERENCE_PREFIX, StringComparison.Ordinal))
                {
                        return false;
                }

                string remainder = rawValue[DomainConstants.PICTURE_REFERENCE_PREFIX.Length..];
                if (!TrySplitNodeAndGuid(
                        input: remainder,
                        separator: DomainConstants.PICTURE_REFERENCE_SEGMENT_SEPARATOR,
                        storageNodeId: out string storageNodeId,
                        imageId: out Guid imageId))
                {
                        return false;
                }

                reference = new PictureReference(storageNodeId, imageId);
                return true;
        }

        /// <summary>
        /// Parses an external asset identifier (<c>{node}_{guid}</c>) into its storage node identifier and image GUID.
        /// </summary>
        /// <param name="externalId">The external asset identifier to parse.</param>
        /// <returns>A tuple containing the validated storage node identifier and image GUID.</returns>
        /// <exception cref="DomainValidationException">Thrown when <paramref name="externalId"/> is blank or malformed.</exception>
        public static (string StorageNodeId, Guid ImageId) ParseExternalId(string? externalId)
        {
                if (string.IsNullOrWhiteSpace(externalId))
                {
                        throw new DomainValidationException("External ID cannot be null or whitespace.");
                }

                if (!TrySplitNodeAndGuid(
                        input: externalId,
                        separator: DomainConstants.EXTERNAL_IMAGE_ID_SEPARATOR,
                        storageNodeId: out string storageNodeId,
                        imageId: out Guid imageId))
                {
                        throw new DomainValidationException($"Invalid external ID format: '{externalId}'.");
                }

                return (storageNodeId, imageId);
        }

        /// <summary>
        /// Scans a template JSON payload for unique managed picture handles while rejecting inline base64 image data URIs.
        /// </summary>
        /// <param name="jsonPayload">The raw template JSON payload to scan.</param>
        /// <returns>A deduplicated list of <see cref="PictureReference"/> values discovered in <paramref name="jsonPayload"/>.</returns>
        /// <exception cref="DomainValidationException">Thrown when <paramref name="jsonPayload"/> is null or contains embedded base64 image data URIs.</exception>
        public static List<PictureReference> ExtractReferences(string? jsonPayload)
        {
                if (jsonPayload is null)
                {
                        throw new DomainValidationException("JSON payload cannot be null.");
                }

                // Block inline base64 images before scanning handles so large binary blobs cannot be embedded in template JSON and bypass ImageEntity quotas.
                if (ContainsEmbeddedBase64Image(jsonPayload))
                {
                        throw new DomainValidationException(
                                "Embedded base64 image data URIs are not permitted in JSON payloads.");
                }

                // Regex + HashSet avoids building a full JsonDocument tree and works regardless of how deeply nested the template JSON structure is.
                HashSet<PictureReference> uniqueReferences = [];
                foreach (Match match in PICTURE_HANDLE_REGEX.Matches(jsonPayload))
                {
                        string nodeId = match.Groups["node"].Value;
                        string guidText = match.Groups["guid"].Value;

                        // Re-check node length and Guid.Empty because the regex intentionally leaves max length (32) and all-zero GUID checks to C#.
                        if (IsValidStorageNodeId(nodeId) && TryParseGuid(guidText, out Guid imageId))
                        {
                                uniqueReferences.Add(new PictureReference(nodeId, imageId));
                        }
                }

                return [.. uniqueReferences];
        }

        /// <summary>
        /// Validates and trims a storage node identifier according to shard routing-key rules.
        /// </summary>
        /// <param name="storageNodeId">The raw storage node identifier to validate.</param>
        /// <returns>The trimmed, valid storage node identifier.</returns>
        /// <exception cref="DomainValidationException">Thrown when <paramref name="storageNodeId"/> is blank, too long, or uses disallowed characters.</exception>
        internal static string ValidateStorageNodeId(string? storageNodeId)
        {
                if (string.IsNullOrWhiteSpace(storageNodeId))
                {
                        throw new DomainValidationException("Node ID cannot be null or whitespace.");
                }

                string trimmedStorageNodeId = storageNodeId.Trim();
                if (!IsValidStorageNodeId(trimmedStorageNodeId))
                {
                        throw new DomainValidationException(
                                $"Node ID '{trimmedStorageNodeId}' must be at most {DomainConstants.MAX_STORAGE_NODE_ID_LENGTH} characters and use lowercase alphanumeric segments separated by single hyphens.");
                }

                return trimmedStorageNodeId;
        }

        /// <summary>
        /// Builds the anchored regular expression used to validate standalone storage node identifiers.
        /// </summary>
        /// <returns>A compiled, culture-invariant <see cref="Regex"/> matching an entire node ID string.</returns>
        private static Regex BuildStandaloneNodeIdRegex()
        {
                // Wraps DomainConstants.STORAGE_NODE_ID_PATTERN with ^ and $ so the whole string must match, not just a substring.
                string anchoredPattern = $"^{DomainConstants.STORAGE_NODE_ID_PATTERN}$";
                return new Regex(
                        pattern: anchoredPattern,
                        options: RegexOptions.Compiled | RegexOptions.CultureInvariant);
        }

        /// <summary>
        /// Builds the regular expression used to locate <c>picture.{node}.{guid}</c> handles inside template JSON.
        /// </summary>
        /// <returns>A compiled, culture-invariant <see cref="Regex"/> with named <c>node</c> and <c>guid</c> capture groups.</returns>
        private static Regex BuildPictureHandleRegex()
        {
                // Negative lookbehind (?<![a-zA-Z0-9_]) ensures we do not match inside another word like "mypicture.node1.<guid>".
                string leadingBoundary = "(?<![a-zA-Z0-9_])";
                string escapedPrefix = Regex.Escape(DomainConstants.PICTURE_REFERENCE_PREFIX);
                string escapedSeparator = Regex.Escape(DomainConstants.PICTURE_REFERENCE_SEGMENT_SEPARATOR.ToString());

                // Named groups (?<node>...) and (?<guid>...) let ExtractReferences read both parts directly without splitting the string a second time.
                string nodeGroup = $"(?<node>{DomainConstants.STORAGE_NODE_ID_PATTERN})";

                // Matches standard 8-4-4-4-12 hyphenated GUIDs (case-insensitive hex).
                // Negative lookahead (?![0-9a-fA-F-]) prevents matching a typo with extra trailing hex characters (e.g., 37 hex chars matching its first 36).
                string guidGroup = @"(?<guid>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})(?![0-9a-fA-F-])";

                string fullPattern = $"{leadingBoundary}{escapedPrefix}{nodeGroup}{escapedSeparator}{guidGroup}";
                return new Regex(
                        pattern: fullPattern,
                        options: RegexOptions.Compiled | RegexOptions.CultureInvariant);
        }

        /// <summary>
        /// Splits a two-segment <c>{node}{separator}{guid}</c> string and validates both components.
        /// </summary>
        /// <param name="input">The input string containing node ID and GUID separated by <paramref name="separator"/>.</param>
        /// <param name="separator">The single-character delimiter separating the node ID and GUID.</param>
        /// <param name="storageNodeId">When this method returns <see langword="true"/>, contains the validated storage node identifier.</param>
        /// <param name="imageId">When this method returns <see langword="true"/>, contains the parsed non-empty image GUID.</param>
        /// <returns><see langword="true"/> if both segments exist and pass validation; otherwise <see langword="false"/>.</returns>
        private static bool TrySplitNodeAndGuid(string? input, char separator, out string storageNodeId, out Guid imageId)
        {
                storageNodeId = string.Empty;
                imageId = Guid.Empty;

                if (string.IsNullOrEmpty(input))
                {
                        return false;
                }

                // Limit split to 2 parts so any extra separators remain inside parts[1] and fail GUID validation.
                string[] parts = input.Split(separator, EXPECTED_SEGMENT_COUNT);
                if (parts.Length != EXPECTED_SEGMENT_COUNT)
                {
                        return false;
                }

                string candidateStorageNodeId = parts[0];
                string guidText = parts[1];
                if (!IsValidStorageNodeId(candidateStorageNodeId) || !TryParseGuid(guidText, out imageId))
                {
                        return false;
                }

                storageNodeId = candidateStorageNodeId;
                return true;
        }

        /// <summary>
        /// Determines whether a candidate string satisfies length and character rules for a storage node identifier.
        /// </summary>
        /// <param name="storageNodeId">The candidate storage node identifier.</param>
        /// <returns><see langword="true"/> if valid; otherwise <see langword="false"/>.</returns>
        private static bool IsValidStorageNodeId(string? storageNodeId)
        {
                if (string.IsNullOrEmpty(storageNodeId))
                {
                        return false;
                }

                // Enforce max length in C# so DomainConstants.MAX_STORAGE_NODE_ID_LENGTH stays the single source of truth instead of hardcoding {1,32} in regex.
                return storageNodeId.Length <= DomainConstants.MAX_STORAGE_NODE_ID_LENGTH
                        && NODE_ID_REGEX.IsMatch(storageNodeId);
        }

        /// <summary>
        /// Parses an exact 36-character hyphenated GUID and rejects <see cref="Guid.Empty"/>.
        /// </summary>
        /// <param name="guidText">The candidate GUID string.</param>
        /// <param name="imageId">When this method returns <see langword="true"/>, contains the parsed non-empty GUID.</param>
        /// <returns><see langword="true"/> if <paramref name="guidText"/> is a valid non-empty hyphenated GUID; otherwise <see langword="false"/>.</returns>
        private static bool TryParseGuid(string guidText, out Guid imageId)
        {
                imageId = Guid.Empty;

                // Check exact length before TryParseExact so alternative formats like braces '{...}' or compact 32-digit 'N' strings are rejected consistently.
                if (guidText.Length != DomainConstants.HYPHENATED_GUID_LENGTH)
                {
                        return false;
                }

                return Guid.TryParseExact(guidText, DomainConstants.GUID_FORMAT_HYPHENATED, out imageId)
                        && imageId != Guid.Empty;
        }

        /// <summary>
        /// Checks whether a JSON payload contains raw or JSON-escaped base64 image data URIs.
        /// </summary>
        /// <param name="jsonPayload">The JSON payload to inspect.</param>
        /// <returns><see langword="true"/> if an embedded base64 image marker is detected; otherwise <see langword="false"/>.</returns>
        private static bool ContainsEmbeddedBase64Image(string jsonPayload)
        {
                return jsonPayload.Contains(DomainConstants.EMBEDDED_BASE64_DATA_URI_MARKER, StringComparison.OrdinalIgnoreCase)
                        || jsonPayload.Contains(DomainConstants.ESCAPED_EMBEDDED_BASE64_DATA_URI_MARKER, StringComparison.OrdinalIgnoreCase);
        }
}
