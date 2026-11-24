using ContentService.Sdk.Common;

namespace ContentService.Sdk.Models;

/// <summary>
/// Query criteria for browsing, filtering, and searching exhibition templates via <see cref="Clients.IContentServiceTemplateClient"/>.
/// </summary>
public sealed record class ContentTemplateQuery
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
        /// Gets an optional filter restricting results to a specific <see cref="ContentTemplateComponentType"/>.
        /// </summary>
        public ContentTemplateComponentType? ComponentType { get; init; }

        /// <summary>
        /// Gets optional search text matched against template display names.
        /// </summary>
        public string? SearchTerm { get; init; }
}

