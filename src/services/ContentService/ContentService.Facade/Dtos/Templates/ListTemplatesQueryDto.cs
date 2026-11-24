using ContentService.Facade.Common;

namespace ContentService.Facade.Dtos;

/// <summary>
/// Query DTO for retrieving a paginated list of exhibition templates.
/// </summary>
public sealed record class ListTemplatesQueryDto
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
        /// Gets an optional filter restricting results to a specific <see cref="TemplateComponentTypeDto"/>.
        /// </summary>
        public TemplateComponentTypeDto? ComponentType { get; init; }

        /// <summary>
        /// Gets optional search text matched against template display names.
        /// </summary>
        public string? SearchTerm { get; init; }
}

