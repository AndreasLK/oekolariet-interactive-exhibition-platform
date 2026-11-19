using System.Text.RegularExpressions;
using ContentService.Domain.Common;
using ContentService.Domain.Exceptions;

namespace ContentService.Domain.ValueObjects;

public readonly record struct PictureHandle
{
        private static readonly Regex NODE_ID_REGEX = new(
                pattern: DomainConstants.NODE_ID_VALIDATION_PATTERN,
                options: RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex PICTURE_HANDLE_REGEX = new(
                pattern: @"picture\.[a-z0-9]+(?:-[a-z0-9]+)*\.[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}(?![0-9a-fA-F-])",
                options: RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public PictureHandle(string nodeId, Guid imageId)
        {
                string validatedNodeId = ValidateNodeId(nodeId: nodeId);
                if (imageId == Guid.Empty)
                {
                        throw new DomainValidationException(message: "Image ID cannot be empty.");
                }

                this.NodeId = validatedNodeId;
                this.ImageId = imageId;
        }

        public string NodeId { get; }

        public Guid ImageId { get; }

        // Embedding NodeId inside both the picture handle and external asset ID lets the Content Gateway route requests straight to the owning MSSQL shard without UI Editor or Skærm Server ever knowing how many instances exist.
        public override string ToString()
        {
                return $"{DomainConstants.PICTURE_HANDLE_PREFIX}{this.NodeId}{DomainConstants.PICTURE_HANDLE_SEGMENT_SEPARATOR}{this.ImageId.ToString(format: DomainConstants.GUID_FORMAT_HYPHENATED)}";
        }

        public string ToExternalImageId()
        {
                return $"{this.NodeId}{DomainConstants.ASSET_ID_SEPARATOR}{this.ImageId.ToString(format: DomainConstants.GUID_FORMAT_HYPHENATED)}";
        }

        public static PictureHandle Parse(string rawValue)
        {
                if (!TryParse(rawValue: rawValue, handle: out PictureHandle handle))
                {
                        throw new DomainValidationException(
                                message: $"Invalid picture handle format: '{rawValue}'.");
                }

                return handle;
        }

        public static bool TryParse(string rawValue, out PictureHandle handle)
        {
                handle = default;
                if (string.IsNullOrEmpty(value: rawValue))
                {
                        return false;
                }

                return TryParseSpan(span: rawValue.AsSpan(), handle: out handle);
        }

        public static (string NodeId, Guid AssetId) ParseExternalId(string externalId)
        {
                if (string.IsNullOrWhiteSpace(value: externalId))
                {
                        throw new DomainValidationException(message: "External ID cannot be null or whitespace.");
                }

                ReadOnlySpan<char> span = externalId.AsSpan();
                int separatorIndex = span.IndexOf(value: DomainConstants.ASSET_ID_SEPARATOR);
                if (separatorIndex <= 0 || separatorIndex >= span.Length - 1)
                {
                        throw new DomainValidationException(
                                message: $"Invalid external ID format: '{externalId}'.");
                }

                ReadOnlySpan<char> nodeIdSpan = span.Slice(start: 0, length: separatorIndex);
                ReadOnlySpan<char> guidSpan = span.Slice(start: separatorIndex + 1);
                if (!IsValidNodeIdSpan(nodeIdSpan: nodeIdSpan) || !TryParseGuidSpan(guidSpan: guidSpan, assetId: out Guid assetId))
                {
                        throw new DomainValidationException(
                                message: $"Invalid external ID format: '{externalId}'.");
                }

                return (nodeIdSpan.ToString(), assetId);
        }

        public static List<PictureHandle> ExtractHandles(string jsonPayload)
        {
                if (jsonPayload is null)
                {
                        throw new DomainValidationException(message: "JSON payload cannot be null.");
                }

                if (ContainsEmbeddedBase64Image(jsonPayload: jsonPayload))
                {
                        throw new DomainValidationException(
                                message: "Embedded base64 image data URIs are not permitted in JSON payloads.");
                }

                HashSet<PictureHandle> uniqueHandles = [];
                ReadOnlySpan<char> payloadSpan = jsonPayload.AsSpan();

                foreach (ValueMatch match in PICTURE_HANDLE_REGEX.EnumerateMatches(input: payloadSpan))
                {
                        ReadOnlySpan<char> candidateSpan = payloadSpan.Slice(start: match.Index, length: match.Length);
                        if (TryParseSpan(span: candidateSpan, handle: out PictureHandle handle))
                        {
                                uniqueHandles.Add(item: handle);
                        }
                }

                return [.. uniqueHandles];
        }

        internal static string ValidateNodeId(string nodeId)
        {
                if (string.IsNullOrWhiteSpace(value: nodeId))
                {
                        throw new DomainValidationException(message: "Node ID cannot be null or whitespace.");
                }

                string trimmedNodeId = nodeId.Trim();
                if (!IsValidNodeIdSpan(nodeIdSpan: trimmedNodeId.AsSpan()))
                {
                        throw new DomainValidationException(
                                message: $"Node ID '{trimmedNodeId}' must be at most {DomainConstants.MAX_NODE_ID_LENGTH} characters and use lowercase alphanumeric segments separated by single hyphens.");
                }

                return trimmedNodeId;
        }

        private static bool TryParseSpan(ReadOnlySpan<char> span, out PictureHandle handle)
        {
                handle = default;
                ReadOnlySpan<char> prefix = DomainConstants.PICTURE_HANDLE_PREFIX.AsSpan();
                if (!span.StartsWith(value: prefix, comparisonType: StringComparison.Ordinal))
                {
                        return false;
                }

                ReadOnlySpan<char> remainder = span.Slice(start: prefix.Length);
                int separatorIndex = remainder.IndexOf(value: DomainConstants.PICTURE_HANDLE_SEGMENT_SEPARATOR);
                if (separatorIndex <= 0 || separatorIndex >= remainder.Length - 1)
                {
                        return false;
                }

                ReadOnlySpan<char> nodeIdSpan = remainder.Slice(start: 0, length: separatorIndex);
                ReadOnlySpan<char> guidSpan = remainder.Slice(start: separatorIndex + 1);
                if (!IsValidNodeIdSpan(nodeIdSpan: nodeIdSpan) || !TryParseGuidSpan(guidSpan: guidSpan, assetId: out Guid imageId))
                {
                        return false;
                }

                handle = new PictureHandle(nodeId: nodeIdSpan.ToString(), imageId: imageId);
                return true;
        }

        private static bool IsValidNodeIdSpan(ReadOnlySpan<char> nodeIdSpan)
        {
                return nodeIdSpan.Length > 0
                        && nodeIdSpan.Length <= DomainConstants.MAX_NODE_ID_LENGTH
                        && NODE_ID_REGEX.IsMatch(input: nodeIdSpan);
        }

        private static bool TryParseGuidSpan(ReadOnlySpan<char> guidSpan, out Guid assetId)
        {
                assetId = Guid.Empty;
                if (guidSpan.Length != DomainConstants.GUID_D_LENGTH)
                {
                        return false;
                }

                return Guid.TryParseExact(input: guidSpan, format: DomainConstants.GUID_FORMAT_HYPHENATED, result: out assetId)
                        && assetId != Guid.Empty;
        }

        private static bool ContainsEmbeddedBase64Image(string jsonPayload)
        {
                return jsonPayload.Contains(value: DomainConstants.EMBEDDED_BASE64_DATA_URI_MARKER, comparisonType: StringComparison.OrdinalIgnoreCase)
                        || jsonPayload.Contains(value: DomainConstants.ESCAPED_EMBEDDED_BASE64_DATA_URI_MARKER, comparisonType: StringComparison.OrdinalIgnoreCase);
        }
}
