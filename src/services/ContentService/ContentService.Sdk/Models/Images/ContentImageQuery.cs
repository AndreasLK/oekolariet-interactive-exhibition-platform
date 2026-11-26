using ContentService.Sdk.Common;

namespace ContentService.Sdk.Models;

/// <summary>
/// Query criteria for browsing, searching, and filtering exhibition images via <c>IContentServiceImageClient</c>.
/// </summary>
public sealed record class ContentImageQuery
{
        /// <summary>
        /// Gets the 1-based page number to retrieve.
        /// </summary>
        public int Page { get; init; } = SdkConstants.DEFAULT_PAGE;

        /// <summary>
        /// Gets the maximum number of image records per page.
        /// </summary>
        public int PageSize { get; init; } = SdkConstants.DEFAULT_PAGE_SIZE;

        /// <summary>
        /// Gets the optional case-insensitive search filter across file name, alt text, and tags.
        /// </summary>
        public string? SearchTerm { get; init; }

        /// <summary>
        /// Gets the optional normalized recursive album path filter.
        /// </summary>
        public string? AlbumPath { get; init; }

        /// <summary>
        /// Gets a value indicating whether nested sub-albums beneath <see cref="AlbumPath"/> are included.
        /// </summary>
        public bool IncludeSubalbums { get; init; } = true;
}

