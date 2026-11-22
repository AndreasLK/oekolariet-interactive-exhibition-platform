using System.Text;

namespace ContentService.Components.Internal;

/// <summary>
/// Pure helper routines for normalizing, extracting, combining, and labeling recursive album paths in UI components.
/// </summary>
internal static class AlbumPathNormalizer
{
        private const char ALBUM_SEPARATOR = '/';
        private const char WINDOWS_SEPARATOR = '\\';
        private const string CURRENT_SEGMENT = ".";
        private const string PARENT_SEGMENT = "..";

        /// <summary>
        /// Normalizes an optional recursive album path (forward slashes, trimmed, safe segments) or returns <see langword="null"/> when blank.
        /// </summary>
        /// <param name="rawAlbumPath">The raw album path entered by the user or passed from a host.</param>
        /// <returns>The normalized album path, or <see langword="null"/> when blank or invalid.</returns>
        /// <remarks>
        /// Unlike <c>AlbumPath.NormalizeOptional</c> in the Domain layer (which throws <c>DomainValidationException</c> for bad input),
        /// this UI helper quietly drops <c>.</c> and <c>..</c> segments so user typing or browser folder metadata never crashes a Razor render.
        /// </remarks>
        public static string? Normalize(string? rawAlbumPath)
        {
                if (string.IsNullOrWhiteSpace(value: rawAlbumPath))
                {
                        return null;
                }

                // Normalize to NFC Unicode (so macOS NFD "æ/ø/å" matches Windows NFC "æ/ø/å") and convert Windows '\' separators to '/'.
                string unified = rawAlbumPath
                        .Normalize(normalizationForm: NormalizationForm.FormC)
                        .Replace(oldChar: WINDOWS_SEPARATOR, newChar: ALBUM_SEPARATOR)
                        .Trim()
                        .Trim(trimChar: ALBUM_SEPARATOR);

                if (string.IsNullOrEmpty(value: unified))
                {
                        return null;
                }

                string[] rawSegments = unified.Split(separator: ALBUM_SEPARATOR, options: StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                List<string> safeSegments = new(capacity: rawSegments.Length);

                foreach (string segment in rawSegments)
                {
                        // Strip relative path-traversal segments rather than throwing inside UI event handlers.
                        if (segment == CURRENT_SEGMENT || segment == PARENT_SEGMENT)
                        {
                                continue;
                        }

                        safeSegments.Add(item: segment);
                }

                return safeSegments.Count == 0
                        ? null
                        : string.Join(separator: ALBUM_SEPARATOR, values: safeSegments);
        }

        /// <summary>
        /// Extracts the normalized directory prefix from a browser <c>RelativePath</c> (e.g., <c>Madaffald/Frugt/banana.png</c> -> <c>Madaffald/Frugt</c>).
        /// </summary>
        /// <param name="relativeFilePath">The relative path supplied by <c>SelectedImageFile.RelativePath</c>.</param>
        /// <returns>The normalized directory path, or <see langword="null"/> when the file has no folder prefix.</returns>
        public static string? ExtractRelativeDirectory(string? relativeFilePath)
        {
                if (string.IsNullOrWhiteSpace(value: relativeFilePath))
                {
                        return null;
                }

                string unified = relativeFilePath
                        .Normalize(normalizationForm: NormalizationForm.FormC)
                        .Replace(oldChar: WINDOWS_SEPARATOR, newChar: ALBUM_SEPARATOR)
                        .Trim()
                        .TrimStart(trimChar: ALBUM_SEPARATOR);

                // If there is no '/' (index -1) or it was at index 0, the file sits at the root with no parent folder to extract.
                int lastSeparator = unified.LastIndexOf(value: ALBUM_SEPARATOR);
                if (lastSeparator <= 0)
                {
                        return null;
                }

                return Normalize(rawAlbumPath: unified[..lastSeparator]);
        }

        /// <summary>
        /// Combines a base album path with an uploaded sub-folder path cleanly.
        /// </summary>
        /// <param name="baseAlbumPath">The active or default album path.</param>
        /// <param name="subAlbumPath">The nested folder path extracted from a folder upload.</param>
        /// <returns>The combined normalized album path, or <see langword="null"/> if both are empty.</returns>
        public static string? Combine(
                string? baseAlbumPath,
                string? subAlbumPath)
        {
                string? normalizedBase = Normalize(rawAlbumPath: baseAlbumPath);
                string? normalizedSub = Normalize(rawAlbumPath: subAlbumPath);

                if (normalizedBase is null)
                {
                        return normalizedSub;
                }

                if (normalizedSub is null)
                {
                        return normalizedBase;
                }

                return $"{normalizedBase}{ALBUM_SEPARATOR}{normalizedSub}";
        }

        /// <summary>
        /// Extracts the trailing folder display name from a full album path (e.g., <c>Madaffald/Frugt</c> -> <c>Frugt</c>).
        /// </summary>
        /// <param name="fullAlbumPath">The full normalized album path.</param>
        /// <returns>The final segment label.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="fullAlbumPath"/> is <see langword="null"/>.</exception>
        public static string FormatLeafName(string fullAlbumPath)
        {
                ArgumentNullException.ThrowIfNull(argument: fullAlbumPath);

                int lastSlash = fullAlbumPath.LastIndexOf(value: ALBUM_SEPARATOR);
                return lastSlash >= 0 ? fullAlbumPath[(lastSlash + 1)..] : fullAlbumPath;
        }
}

