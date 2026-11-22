using ContentService.Sdk.Common;

namespace ContentService.Sdk.Models;

/// <summary>
/// Query criteria for browsing, searching, and album-filtering exhibition images via <see cref="Clients.IContentServiceImageClient"/>.
/// </summary>
public sealed record class ContentImageQuery
{
        /// <summary>
        /// Gets the 1-based page number to retrieve.
        /// </summary>
        public int Page { get; init; } = SdkConstants.DEFAULT_PAGE;

        /// <summary>
        /// Gets the maximum number of items per page.
        /// </summary>
        public int PageSize { get; init; } = SdkConstants.DEFAULT_PAGE_SIZE;

        /// <summary>
        /// Gets optional search text matched across file names, alt text, and tags.
        /// </summary>
        public string? SearchTerm { get; init; }

        /// <summary>
        /// Gets an optional recursive album path filter (e.g., <c>Madaffald</c>).
        /// </summary>
        public string? AlbumPath { get; init; }

        /// <summary>
        /// Gets a value indicating whether nested sub-albums beneath <see cref="AlbumPath"/> are included.
        /// </summary>
        public bool IncludeSubalbums { get; init; } = true;
}

