using ContentService.Application.Common;

namespace ContentService.Application.Models;

/// <summary>
/// Query criteria for retrieving a paginated list of active exhibition images.
/// </summary>
public sealed record class ListImagesQuery
{
        /// <summary>
        /// Gets the requested 1-based page number.
        /// </summary>
        public int Page { get; init; } = ApplicationConstants.DEFAULT_PAGE;

        /// <summary>
        /// Gets the requested number of items per page.
        /// </summary>
        public int PageSize { get; init; } = ApplicationConstants.DEFAULT_PAGE_SIZE;

        /// <summary>
        /// Gets optional search text matched against image file names, alt text, and tags.
        /// </summary>
        public string? SearchTerm { get; init; }

        /// <summary>
        /// Gets optional recursive album path filter.
        /// </summary>
        public string? AlbumPath { get; init; }

        /// <summary>
        /// Gets a value indicating whether nested sub-albums under <see cref="AlbumPath"/> are included in results.
        /// </summary>
        public bool IncludeSubalbums { get; init; } = true;
}

