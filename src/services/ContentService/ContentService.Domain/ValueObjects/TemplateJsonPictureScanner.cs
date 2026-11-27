using System.Text.RegularExpressions;
using ContentService.Domain.Common;
using ContentService.Domain.Exceptions;

namespace ContentService.Domain.ValueObjects;

/// <summary>
/// Scans template JSON payloads for managed <c>picture.{node}.{guid}</c> handles while rejecting embedded base64 image data URIs.
/// </summary>
internal static class TemplateJsonPictureScanner
{
        /// <summary>
        /// Regex locating <c>picture.{node}.{guid}</c> handles inside template JSON payloads.
        /// </summary>
        private static readonly Regex PICTURE_HANDLE_REGEX = BuildPictureHandleRegex();

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
                if (ContainsEmbeddedBase64Image(jsonPayload: jsonPayload))
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
                        if (PictureReferenceParser.IsValidStorageNodeId(storageNodeId: nodeId)
                                && PictureReferenceParser.TryParseGuid(guidText: guidText, imageId: out Guid imageId))
                        {
                                uniqueReferences.Add(new PictureReference(storageNodeId: nodeId, imageId: imageId));
                        }
                }

                return [.. uniqueReferences];
        }

        /// <summary>
        /// Builds the regular expression used to locate <c>picture.{node}.{guid}</c> handles inside template JSON.
        /// </summary>
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
        /// Checks whether a JSON payload contains raw or JSON-escaped base64 image data URIs.
        /// </summary>
        private static bool ContainsEmbeddedBase64Image(string jsonPayload)
        {
                return jsonPayload.Contains(value: DomainConstants.EMBEDDED_BASE64_DATA_URI_MARKER, comparisonType: StringComparison.OrdinalIgnoreCase)
                        || jsonPayload.Contains(value: DomainConstants.ESCAPED_EMBEDDED_BASE64_DATA_URI_MARKER, comparisonType: StringComparison.OrdinalIgnoreCase);
        }
}

