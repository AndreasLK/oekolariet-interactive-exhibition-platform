using System.Text.RegularExpressions;
using ContentService.Domain.Common;
using ContentService.Domain.Exceptions;

namespace ContentService.Domain.ValueObjects;

/// <summary>
/// Parses and validates shard-aware <see cref="PictureReference"/> handles (<c>picture.{node}.{guid}</c>) and external asset identifiers (<c>{node}_{guid}</c>).
/// </summary>
internal static class PictureReferenceParser
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
        /// Parses a template picture handle (<c>picture.{node}.{guid}</c>) into a <see cref="PictureReference"/>.
        /// </summary>
        /// <param name="rawValue">The raw handle string to parse.</param>
        /// <returns>The parsed <see cref="PictureReference"/>.</returns>
        /// <exception cref="DomainValidationException">Thrown when <paramref name="rawValue"/> is null or does not match the required handle format.</exception>
        public static PictureReference Parse(string? rawValue)
        {
                if (!TryParse(rawValue: rawValue, reference: out PictureReference? reference))
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
                        || !rawValue.StartsWith(value: DomainConstants.PICTURE_REFERENCE_PREFIX, comparisonType: StringComparison.Ordinal))
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

                reference = new PictureReference(storageNodeId: storageNodeId, imageId: imageId);
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
                if (!TryParseExternalId(
                        externalId: externalId,
                        storageNodeId: out string storageNodeId,
                        imageId: out Guid imageId))
                {
                        throw new DomainValidationException($"Invalid external ID format: '{externalId}'.");
                }

                return (storageNodeId, imageId);
        }

        /// <summary>
        /// Attempts to parse an external asset identifier (<c>{node}_{guid}</c>) without throwing on invalid input.
        /// </summary>
        /// <param name="externalId">The external asset identifier to parse.</param>
        /// <param name="storageNodeId">When this method returns <see langword="true"/>, contains the validated storage node identifier.</param>
        /// <param name="imageId">When this method returns <see langword="true"/>, contains the parsed non-empty image GUID.</param>
        /// <returns><see langword="true"/> if <paramref name="externalId"/> is valid; otherwise <see langword="false"/>.</returns>
        public static bool TryParseExternalId(string? externalId, out string storageNodeId, out Guid imageId)
        {
                storageNodeId = string.Empty;
                imageId = Guid.Empty;

                if (string.IsNullOrWhiteSpace(externalId))
                {
                        return false;
                }

                // Trim outer whitespace before splitting so accidental surrounding spaces in route/header inputs do not fail valid external IDs.
                return TrySplitNodeAndGuid(
                        input: externalId.Trim(),
                        separator: DomainConstants.EXTERNAL_IMAGE_ID_SEPARATOR,
                        storageNodeId: out storageNodeId,
                        imageId: out imageId);
        }

        /// <summary>
        /// Validates and trims a storage node identifier according to shard routing-key rules.
        /// </summary>
        /// <param name="storageNodeId">The raw storage node identifier to validate.</param>
        /// <returns>The trimmed, valid storage node identifier.</returns>
        /// <exception cref="DomainValidationException">Thrown when <paramref name="storageNodeId"/> is blank, too long, or uses disallowed characters.</exception>
        public static string ValidateStorageNodeId(string? storageNodeId)
        {
                if (string.IsNullOrWhiteSpace(storageNodeId))
                {
                        throw new DomainValidationException("Node ID cannot be null or whitespace.");
                }

                string trimmedStorageNodeId = storageNodeId.Trim();
                if (!IsValidStorageNodeId(storageNodeId: trimmedStorageNodeId))
                {
                        throw new DomainValidationException(
                                $"Node ID '{trimmedStorageNodeId}' must be at most {DomainConstants.MAX_STORAGE_NODE_ID_LENGTH} characters and use lowercase alphanumeric segments separated by single hyphens.");
                }

                return trimmedStorageNodeId;
        }

        /// <summary>
        /// Determines whether a candidate string satisfies length and character rules for a storage node identifier.
        /// </summary>
        /// <param name="storageNodeId">The candidate storage node identifier.</param>
        /// <returns><see langword="true"/> if valid; otherwise <see langword="false"/>.</returns>
        public static bool IsValidStorageNodeId(string? storageNodeId)
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
        public static bool TryParseGuid(string guidText, out Guid imageId)
        {
                imageId = Guid.Empty;

                // Check exact length before TryParseExact so alternative formats like braces '{...}' or compact 32-digit 'N' strings are rejected consistently.
                if (guidText.Length != DomainConstants.HYPHENATED_GUID_LENGTH)
                {
                        return false;
                }

                return Guid.TryParseExact(input: guidText, format: DomainConstants.GUID_FORMAT_HYPHENATED, result: out imageId)
                        && imageId != Guid.Empty;
        }

        /// <summary>
        /// Splits a two-segment <c>{node}{separator}{guid}</c> string and validates both components.
        /// </summary>
        private static bool TrySplitNodeAndGuid(string? input, char separator, out string storageNodeId, out Guid imageId)
        {
                storageNodeId = string.Empty;
                imageId = Guid.Empty;

                if (string.IsNullOrEmpty(input))
                {
                        return false;
                }

                // Limit split to 2 parts so any extra separators remain inside parts[1] and fail GUID validation.
                string[] parts = input.Split(separator: separator, count: EXPECTED_SEGMENT_COUNT);
                if (parts.Length != EXPECTED_SEGMENT_COUNT)
                {
                        return false;
                }

                string candidateStorageNodeId = parts[0];
                string guidText = parts[1];
                if (!IsValidStorageNodeId(storageNodeId: candidateStorageNodeId) || !TryParseGuid(guidText: guidText, imageId: out imageId))
                {
                        return false;
                }

                storageNodeId = candidateStorageNodeId;
                return true;
        }

        /// <summary>
        /// Builds the anchored regular expression used to validate standalone storage node identifiers.
        /// </summary>
        private static Regex BuildStandaloneNodeIdRegex()
        {
                // Wraps DomainConstants.STORAGE_NODE_ID_PATTERN with ^ and $ so the whole string must match, not just a substring.
                string anchoredPattern = $"^{DomainConstants.STORAGE_NODE_ID_PATTERN}$";
                return new Regex(
                        pattern: anchoredPattern,
                        options: RegexOptions.Compiled | RegexOptions.CultureInvariant);
        }
}

