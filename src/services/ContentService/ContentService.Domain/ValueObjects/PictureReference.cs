using System.Text.RegularExpressions;
using ContentService.Domain.Common;
using ContentService.Domain.Exceptions;

namespace ContentService.Domain.ValueObjects;

public readonly record struct PictureReference
{
        // Compiled regexes pre-generate IL at startup for faster repeated matching; CultureInvariant avoids locale-specific character rules.
        private static readonly Regex NODE_ID_REGEX = new(
                pattern: DomainConstants.STORAGE_NODE_ID_VALIDATION_PATTERN,
                options: RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Matches 'picture.<node-id>.<guid>' where (?![0-9a-fA-F-]) is a negative lookahead ensuring the GUID isn't followed by extra hex/hyphen chars.
        private static readonly Regex PICTURE_HANDLE_REGEX = new(
                pattern: @"picture\.[a-z0-9]+(?:-[a-z0-9]+)*\.[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}(?![0-9a-fA-F-])",
                options: RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public PictureReference(string storageNodeId, Guid imageId)
        {
                string validatedStorageNodeId = ValidateStorageNodeId(storageNodeId: storageNodeId);
                if (imageId == Guid.Empty)
                {
                        throw new DomainValidationException(message: "Image ID cannot be empty.");
                }

                this.StorageNodeId = validatedStorageNodeId;
                this.ImageId = imageId;
        }

        public string StorageNodeId { get; }

        public Guid ImageId { get; }

        // Embedding StorageNodeId inside both the picture reference and external asset ID lets the Content Gateway route requests straight to the owning MSSQL shard without UI Editor or Skærm Server ever knowing how many instances exist.
        public override string ToString()
        {
                return $"{DomainConstants.PICTURE_REFERENCE_PREFIX}{this.StorageNodeId}{DomainConstants.PICTURE_REFERENCE_SEGMENT_SEPARATOR}{this.ImageId.ToString(format: DomainConstants.GUID_FORMAT_HYPHENATED)}";
        }

        public string ToExternalImageId()
        {
                return $"{this.StorageNodeId}{DomainConstants.EXTERNAL_IMAGE_ID_SEPARATOR}{this.ImageId.ToString(format: DomainConstants.GUID_FORMAT_HYPHENATED)}";
        }

        // Throws DomainValidationException on invalid input; use TryParse when caller wants a non-throwing boolean check.
        public static PictureReference Parse(string rawValue)
        {
                if (!TryParse(rawValue: rawValue, reference: out PictureReference reference))
                {
                        throw new DomainValidationException(
                                message: $"Invalid picture reference format: '{rawValue}'.");
                }

                return reference;
        }

        // Parses 'picture.<storageNodeId>.<guid>' using string.Split instead of ReadOnlySpan<char> so any reader can follow the logic easily.
        public static bool TryParse(string rawValue, out PictureReference reference)
        {
                reference = default;
                if (string.IsNullOrEmpty(value: rawValue)
                        || !rawValue.StartsWith(value: DomainConstants.PICTURE_REFERENCE_PREFIX, comparisonType: StringComparison.Ordinal))
                {
                        return false;
                }

                string remainder = rawValue[DomainConstants.PICTURE_REFERENCE_PREFIX.Length..];
                string[] parts = remainder.Split(separator: DomainConstants.PICTURE_REFERENCE_SEGMENT_SEPARATOR, count: 2);
                if (parts.Length != 2)
                {
                        return false;
                }

                string storageNodeId = parts[0];
                string guidText = parts[1];
                if (!IsValidStorageNodeId(storageNodeId: storageNodeId) || !TryParseGuid(guidText: guidText, imageId: out Guid imageId))
                {
                        return false;
                }

                reference = new PictureReference(storageNodeId: storageNodeId, imageId: imageId);
                return true;
        }

        // Splits '<storageNodeId>_<guid>' using string.Split for clarity over manual span slicing.
        public static (string StorageNodeId, Guid ImageId) ParseExternalId(string externalId)
        {
                if (string.IsNullOrWhiteSpace(value: externalId))
                {
                        throw new DomainValidationException(message: "External ID cannot be null or whitespace.");
                }

                string[] parts = externalId.Split(separator: DomainConstants.EXTERNAL_IMAGE_ID_SEPARATOR, count: 2);
                if (parts.Length != 2)
                {
                        throw new DomainValidationException(
                                message: $"Invalid external ID format: '{externalId}'.");
                }

                string storageNodeId = parts[0];
                string guidText = parts[1];
                if (!IsValidStorageNodeId(storageNodeId: storageNodeId) || !TryParseGuid(guidText: guidText, imageId: out Guid imageId))
                {
                        throw new DomainValidationException(
                                message: $"Invalid external ID format: '{externalId}'.");
                }

                return (storageNodeId, imageId);
        }

        // Scans a JSON payload for 'picture.<storageNodeId>.<guid>' references and deduplicates them.
        public static List<PictureReference> ExtractReferences(string jsonPayload)
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

                HashSet<PictureReference> uniqueReferences = [];
                foreach (Match match in PICTURE_HANDLE_REGEX.Matches(input: jsonPayload))
                {
                        if (TryParse(rawValue: match.Value, reference: out PictureReference reference))
                        {
                                uniqueReferences.Add(item: reference);
                        }
                }

                return [.. uniqueReferences];
        }

        // Centralizes StorageNodeId validation so ImageEntity, TemplateEntity, and PictureReference share the exact same routing-key rules.
        internal static string ValidateStorageNodeId(string storageNodeId)
        {
                if (string.IsNullOrWhiteSpace(value: storageNodeId))
                {
                        throw new DomainValidationException(message: "Node ID cannot be null or whitespace.");
                }

                string trimmedStorageNodeId = storageNodeId.Trim();
                if (!IsValidStorageNodeId(storageNodeId: trimmedStorageNodeId))
                {
                        throw new DomainValidationException(
                                message: $"Node ID '{trimmedStorageNodeId}' must be at most {DomainConstants.MAX_STORAGE_NODE_ID_LENGTH} characters and use lowercase alphanumeric segments separated by single hyphens.");
                }

                return trimmedStorageNodeId;
        }

        private static bool IsValidStorageNodeId(string storageNodeId)
        {
                return storageNodeId.Length > 0
                        && storageNodeId.Length <= DomainConstants.MAX_STORAGE_NODE_ID_LENGTH
                        && NODE_ID_REGEX.IsMatch(input: storageNodeId);
        }

        // Enforces exact hyphenated 36-char GUID format ('D') and rejects Guid.Empty so malformed or zero IDs are caught early.
        private static bool TryParseGuid(string guidText, out Guid imageId)
        {
                imageId = Guid.Empty;
                if (guidText.Length != DomainConstants.HYPHENATED_GUID_LENGTH)
                {
                        return false;
                }

                return Guid.TryParseExact(input: guidText, format: DomainConstants.GUID_FORMAT_HYPHENATED, result: out imageId)
                        && imageId != Guid.Empty;
        }

        // Rejects inline base64 data URIs (both raw and JSON-escaped) so templates must reference managed ImageEntity assets instead.
        private static bool ContainsEmbeddedBase64Image(string jsonPayload)
        {
                return jsonPayload.Contains(value: DomainConstants.EMBEDDED_BASE64_DATA_URI_MARKER, comparisonType: StringComparison.OrdinalIgnoreCase)
                        || jsonPayload.Contains(value: DomainConstants.ESCAPED_EMBEDDED_BASE64_DATA_URI_MARKER, comparisonType: StringComparison.OrdinalIgnoreCase);
        }
}
