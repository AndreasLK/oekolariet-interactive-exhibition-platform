using ContentService.Facade.Common;

namespace ContentService.Facade.Dtos;

/// <summary>
/// Query DTO for retrieving a paginated, filtered catalog of active exhibition images.
/// </summary>
public sealed record class ListImagesQueryDto
{
        /// <summary>
        /// Gets the requested 1-based page number.
        /// </summary>
        public int Page { get; init; } = FacadeConstants.DEFAULT_PAGE;

        /// <summary>
        /// Gets the requested number of items per page.
        /// </summary>
        public int PageSize { get; init; } = FacadeConstants.DEFAULT_PAGE_SIZE;

        /// <summary>
        /// Gets optional search text matched against file name, alt text, and tags.
        /// </summary>
        public string? SearchTerm { get; init; }

        /// <summary>
        /// Gets an optional recursive album path filter.
        /// </summary>
        public string? AlbumPath { get; init; }

        /// <summary>
        /// Gets a value indicating whether nested sub-albums should be included when <see cref="AlbumPath"/> is set.
        /// </summary>
        public bool IncludeSubalbums { get; init; } = true;
}

