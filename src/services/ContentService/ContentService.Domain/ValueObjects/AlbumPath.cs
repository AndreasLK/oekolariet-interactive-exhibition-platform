using System.Text;
using ContentService.Domain.Common;
using ContentService.Domain.Exceptions;

namespace ContentService.Domain.ValueObjects;

/// <summary>
/// Validates and normalizes recursive album paths (e.g., <c>Madaffald/Frugt</c>) with path-traversal protection.
/// </summary>
public static class AlbumPath
{
        private const string CURRENT_DIRECTORY_SEGMENT = ".";
        private const string PARENT_DIRECTORY_SEGMENT = "..";
        private const char WINDOWS_PATH_SEPARATOR = '\\';

        /// <summary>
        /// Normalizes an optional album path using forward slashes and NFC Unicode normalization, returning <see langword="null"/> when blank.
        /// </summary>
        /// <param name="rawAlbumPath">The raw album path supplied by a caller or uploaded folder.</param>
        /// <returns>The normalized slash-delimited album path, or <see langword="null"/> when blank.</returns>
        /// <exception cref="DomainValidationException">Thrown when any segment is invalid, empty (<c>//</c>), traverses directories (<c>..</c>), or exceeds bounds.</exception>
        public static string? NormalizeOptional(string? rawAlbumPath)
        {
                if (string.IsNullOrWhiteSpace(value: rawAlbumPath))
                {
                        return null;
                }

                // Normalize NFC Unicode so macOS NFD and Windows NFC Danish characters (æ/ø/å) hash, match, and query identically in SQL Server.
                // Also unify Windows '\' separators to '/' and trim outer slashes so "/Madaffald\\Frugt/" becomes "Madaffald/Frugt".
                string unified = rawAlbumPath
                        .Normalize(normalizationForm: NormalizationForm.FormC)
                        .Replace(oldChar: WINDOWS_PATH_SEPARATOR, newChar: DomainConstants.ALBUM_PATH_SEPARATOR)
                        .Trim()
                        .Trim(trimChar: DomainConstants.ALBUM_PATH_SEPARATOR);

                if (string.IsNullOrEmpty(value: unified))
                {
                        return null;
                }

                // Split with StringSplitOptions.None (NOT RemoveEmptyEntries) so double slashes like "a//b" are caught as invalid empty segments.
                string[] rawSegments = unified.Split(separator: DomainConstants.ALBUM_PATH_SEPARATOR, options: StringSplitOptions.None);
                if (rawSegments.Length > DomainConstants.MAX_ALBUM_DEPTH)
                {
                        throw new DomainValidationException(
                                $"Album path cannot exceed {DomainConstants.MAX_ALBUM_DEPTH} nested levels.");
                }

                List<string> normalizedSegments = new(capacity: rawSegments.Length);
                foreach (string rawSegment in rawSegments)
                {
                        normalizedSegments.Add(item: ValidateSegment(rawSegment: rawSegment));
                }

                string combined = string.Join(separator: DomainConstants.ALBUM_PATH_SEPARATOR, values: normalizedSegments);
                if (combined.Length > DomainConstants.MAX_ALBUM_PATH_LENGTH)
                {
                        throw new DomainValidationException(
                                $"Album path cannot exceed {DomainConstants.MAX_ALBUM_PATH_LENGTH} characters.");
                }

                return combined;
        }

        /// <summary>
        /// Extracts and normalizes the parent directory portion of a relative uploaded file path (excluding the leaf file name).
        /// </summary>
        /// <param name="relativeFilePath">The browser-supplied relative file path (e.g., <c>Madaffald/Frugt/banana.png</c>).</param>
        /// <returns>The normalized parent album path, or <see langword="null"/> when the file has no directory prefix.</returns>
        public static string? ExtractDirectoryFromRelativeFilePath(string? relativeFilePath)
        {
                if (string.IsNullOrWhiteSpace(value: relativeFilePath))
                {
                        return null;
                }

                string unified = relativeFilePath
                        .Normalize(normalizationForm: NormalizationForm.FormC)
                        .Replace(oldChar: WINDOWS_PATH_SEPARATOR, newChar: DomainConstants.ALBUM_PATH_SEPARATOR)
                        .Trim()
                        .TrimStart(trimChar: DomainConstants.ALBUM_PATH_SEPARATOR);

                // Find the final '/': if missing (-1) or at index 0 ("/file.png"), there is no subfolder prefix.
                int lastSeparatorIndex = unified.LastIndexOf(value: DomainConstants.ALBUM_PATH_SEPARATOR);
                if (lastSeparatorIndex <= 0)
                {
                        return null;
                }

                string directoryPart = unified[..lastSeparatorIndex];
                return NormalizeOptional(rawAlbumPath: directoryPart);
        }

        /// <summary>
        /// Combines an optional base album path with an optional relative sub-album path cleanly.
        /// </summary>
        /// <param name="baseAlbumPath">The active or default base album path.</param>
        /// <param name="subAlbumPath">The nested sub-album path.</param>
        /// <returns>The combined normalized album path, or <see langword="null"/> when both are empty.</returns>
        public static string? Combine(
                string? baseAlbumPath,
                string? subAlbumPath)
        {
                string? normalizedBase = NormalizeOptional(rawAlbumPath: baseAlbumPath);
                string? normalizedSub = NormalizeOptional(rawAlbumPath: subAlbumPath);

                if (normalizedBase is null)
                {
                        return normalizedSub;
                }

                if (normalizedSub is null)
                {
                        return normalizedBase;
                }

                // Re-run NormalizeOptional on the combined path so total depth and total length limits are still enforced across both parts.
                return NormalizeOptional(rawAlbumPath: $"{normalizedBase}{DomainConstants.ALBUM_PATH_SEPARATOR}{normalizedSub}");
        }

        /// <summary>
        /// Validates one folder segment, blocking empty segments, <c>.</c>/<c>..</c> traversal, length overflow, and control characters.
        /// </summary>
        private static string ValidateSegment(string rawSegment)
        {
                string trimmed = rawSegment.Trim();
                if (string.IsNullOrEmpty(value: trimmed))
                {
                        throw new DomainValidationException("Album path cannot contain empty segments.");
                }

                // Block '.' and '..' even though album paths are virtual SQL labels, preventing path-traversal confusion in URLs/exports.
                if (trimmed == CURRENT_DIRECTORY_SEGMENT || trimmed == PARENT_DIRECTORY_SEGMENT)
                {
                        throw new DomainValidationException("Album path cannot contain '.' or '..' traversal segments.");
                }

                if (trimmed.Length > DomainConstants.MAX_ALBUM_SEGMENT_LENGTH)
                {
                        throw new DomainValidationException(
                                $"Album segment '{trimmed}' cannot exceed {DomainConstants.MAX_ALBUM_SEGMENT_LENGTH} characters.");
                }

                foreach (char character in trimmed)
                {
                        if (char.IsControl(c: character))
                        {
                                throw new DomainValidationException("Album path cannot contain control characters.");
                        }
                }

                return trimmed;
        }
}

