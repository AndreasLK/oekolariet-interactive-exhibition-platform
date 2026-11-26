using System.Text;
using ContentService.Domain.Common;
using ContentService.Domain.Exceptions;

namespace ContentService.Domain.ValueObjects;

/// <summary>
/// Validates, deduplicates, and formats normalized comma-separated search tags for <see cref="Entities.ImageEntity"/>.
/// </summary>
public static class ImageTags
{
        /// <summary>
        /// Normalizes an optional comma-separated tag list into lowercase, deduplicated, NFC-normalized tokens, returning <see langword="null"/> when empty.
        /// </summary>
        /// <param name="rawTags">The raw comma-separated tags string, or <see langword="null"/>.</param>
        /// <returns>The normalized comma-separated string, or <see langword="null"/> when no non-empty tags exist.</returns>
        /// <exception cref="DomainValidationException">Thrown when tag count, tag length, or character safety bounds are exceeded.</exception>
        public static string? NormalizeOptional(string? rawTags)
        {
                if (string.IsNullOrWhiteSpace(value: rawTags))
                {
                        return null;
                }

                // Normalize to NFC Unicode before splitting so Danish letters (æ/ø/å) compare reliably regardless of OS input form.
                string[] candidates = rawTags
                        .Normalize(normalizationForm: NormalizationForm.FormC)
                        .Split(separator: DomainConstants.IMAGE_TAG_SEPARATOR, options: StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                if (candidates.Length == 0)
                {
                        return null;
                }

                // Preserve first-seen tag order while deduplicating case-insensitively (e.g., "Plastik, plastik" -> "plastik").
                List<string> deduplicated = new(capacity: candidates.Length);
                HashSet<string> seen = new(comparer: StringComparer.OrdinalIgnoreCase);

                foreach (string candidate in candidates)
                {
                        string normalized = ValidateTag(rawTag: candidate);
                        if (seen.Add(item: normalized))
                        {
                                deduplicated.Add(item: normalized);
                        }
                }

                if (deduplicated.Count > DomainConstants.MAX_IMAGE_TAG_COUNT)
                {
                        throw new DomainValidationException(
                                $"Images cannot have more than {DomainConstants.MAX_IMAGE_TAG_COUNT} tags.");
                }

                string joined = string.Join(separator: DomainConstants.IMAGE_TAG_SEPARATOR, values: deduplicated);
                if (joined.Length > DomainConstants.MAX_IMAGE_TAGS_LENGTH)
                {
                        throw new DomainValidationException(
                                $"Normalized tags cannot exceed {DomainConstants.MAX_IMAGE_TAGS_LENGTH} characters.");
                }

                return joined;
        }

        /// <summary>
        /// Splits a persisted normalized tags string into an immutable read-only list for DTO projections.
        /// </summary>
        /// <param name="normalizedTags">The persisted comma-separated tags column value.</param>
        /// <returns>A read-only list of tag tokens (empty when <paramref name="normalizedTags"/> is blank).</returns>
        public static IReadOnlyList<string> Parse(string? normalizedTags)
        {
                if (string.IsNullOrWhiteSpace(value: normalizedTags))
                {
                        return [];
                }

                return normalizedTags.Split(
                        separator: DomainConstants.IMAGE_TAG_SEPARATOR,
                        options: StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        }

        private static string ValidateTag(string rawTag)
        {
                string lowered = rawTag.Trim().ToLowerInvariant();
                if (lowered.Length > DomainConstants.MAX_SINGLE_TAG_LENGTH)
                {
                        throw new DomainValidationException(
                                $"Tag '{lowered}' cannot exceed {DomainConstants.MAX_SINGLE_TAG_LENGTH} characters.");
                }

                foreach (char character in lowered)
                {
                        if (char.IsControl(c: character))
                        {
                                throw new DomainValidationException("Tags cannot contain control characters.");
                        }
                }

                return lowered;
        }
}

