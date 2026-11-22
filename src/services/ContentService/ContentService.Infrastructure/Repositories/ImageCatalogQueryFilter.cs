using System.Text;
using ContentService.Domain.Common;
using ContentService.Domain.Entities;
using ContentService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ContentService.Infrastructure.Repositories;

/// <summary>
/// Builds EF Core catalog filters and extracts immediate child album paths for <see cref="SqlServerImageRepository"/>.
/// </summary>
internal static class ImageCatalogQueryFilter
{
        /// <summary>
        /// Applies exact or recursive subtree filtering for <paramref name="albumPath"/>.
        /// </summary>
        /// <param name="query">The base image query.</param>
        /// <param name="albumPath">The optional normalized album path filter.</param>
        /// <param name="includeSubalbums">Whether nested sub-albums beneath <paramref name="albumPath"/> should be included.</param>
        /// <returns>The filtered <see cref="IQueryable{T}"/>.</returns>
        public static IQueryable<ImageEntity> ApplyAlbumFilter(
                IQueryable<ImageEntity> query,
                string? albumPath,
                bool includeSubalbums)
        {
                if (string.IsNullOrWhiteSpace(value: albumPath))
                {
                        return query;
                }

                if (!includeSubalbums)
                {
                        return query.Where(image => image.AlbumPath == albumPath);
                }

                // Append trailing slash so filtering 'Madaffald' never accidentally matches a sibling album like 'Madaffald2'.
                string childPrefix = $"{albumPath}{DomainConstants.ALBUM_PATH_SEPARATOR}";
                return query.Where(image =>
                        image.AlbumPath == albumPath
                        || (image.AlbumPath != null && image.AlbumPath.StartsWith(childPrefix)));
        }

        /// <summary>
        /// Applies escaped SQL <c>LIKE</c> matching across file name, alternative text, and tags.
        /// </summary>
        /// <param name="query">The base image query.</param>
        /// <param name="searchTerm">The optional user search query.</param>
        /// <returns>The filtered <see cref="IQueryable{T}"/>.</returns>
        public static IQueryable<ImageEntity> ApplySearchFilter(
                IQueryable<ImageEntity> query,
                string? searchTerm)
        {
                if (string.IsNullOrWhiteSpace(value: searchTerm))
                {
                        return query;
                }

                string escapedTerm = EscapeLikeWildcards(rawTerm: searchTerm.Trim());
                string pattern = $"%{escapedTerm}%";

                return query.Where(image =>
                        EF.Functions.Like(image.FileName, pattern, PersistenceConstants.LIKE_ESCAPE_CHAR)
                        || (image.AltText != null && EF.Functions.Like(image.AltText, pattern, PersistenceConstants.LIKE_ESCAPE_CHAR))
                        || (image.Tags != null && EF.Functions.Like(image.Tags, pattern, PersistenceConstants.LIKE_ESCAPE_CHAR)));
        }

        /// <summary>
        /// Projects distinct descendant album paths into sorted immediate child album paths beneath <paramref name="parentAlbumPath"/>.
        /// </summary>
        /// <param name="parentAlbumPath">The parent album path, or <see langword="null"/> for root-level albums.</param>
        /// <param name="descendantPaths">Distinct stored album paths from SQL Server.</param>
        /// <returns>Sorted immediate child album paths.</returns>
        public static IReadOnlyList<string> ExtractImmediateChildAlbums(
                string? parentAlbumPath,
                IReadOnlyList<string> descendantPaths)
        {
                string prefix = parentAlbumPath is null
                        ? string.Empty
                        : $"{parentAlbumPath}{DomainConstants.ALBUM_PATH_SEPARATOR}";

                HashSet<string> immediateChildren = new(comparer: StringComparer.OrdinalIgnoreCase);

                foreach (string fullPath in descendantPaths)
                {
                        if (prefix.Length > 0 && !fullPath.StartsWith(value: prefix, comparisonType: StringComparison.OrdinalIgnoreCase))
                        {
                                continue;
                        }

                        string remainder = fullPath[prefix.Length..];
                        if (string.IsNullOrWhiteSpace(value: remainder))
                        {
                                continue;
                        }

                        int slashIndex = remainder.IndexOf(value: DomainConstants.ALBUM_PATH_SEPARATOR);
                        string nextSegment = slashIndex >= 0 ? remainder[..slashIndex] : remainder;
                        immediateChildren.Add(item: $"{prefix}{nextSegment}");
                }

                return immediateChildren
                        .OrderBy(keySelector: path => path, comparer: StringComparer.OrdinalIgnoreCase)
                        .ToArray();
        }

        private static string EscapeLikeWildcards(string rawTerm)
        {
                StringBuilder builder = new(capacity: rawTerm.Length);
                foreach (char character in rawTerm)
                {
                        if (character is '%' or '_' or '[' or '\\')
                        {
                                builder.Append(value: PersistenceConstants.LIKE_ESCAPE_CHAR);
                        }

                        builder.Append(value: character);
                }

                return builder.ToString();
        }
}

