using ContentService.Sdk.Common;

namespace ContentService.Sdk.Models;

/// <summary>
/// Query criteria for browsing and filtering exhibition templates via <c>IContentServiceTemplateClient</c>.
/// </summary>
public sealed record class ContentTemplateQuery
{
        /// <summary>
        /// Gets the 1-based page number to retrieve.
        /// </summary>
        public int Page { get; init; } = SdkConstants.DEFAULT_PAGE;

        /// <summary>
        /// Gets the maximum number of template records per page.
        /// </summary>
        public int PageSize { get; init; } = SdkConstants.DEFAULT_PAGE_SIZE;

        /// <summary>
        /// Gets the optional component category filter.
        /// </summary>
        public ContentTemplateComponentType? ComponentType { get; init; }

        /// <summary>
        /// Gets the optional case-insensitive template name search filter.
        /// </summary>
        public string? SearchTerm { get; init; }
}

