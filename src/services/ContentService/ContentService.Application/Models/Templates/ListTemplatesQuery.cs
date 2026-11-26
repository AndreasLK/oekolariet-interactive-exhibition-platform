using ContentService.Application.Common;
using ContentService.Domain.Enums;

namespace ContentService.Application.Models;

/// <summary>
/// Query criteria for retrieving a paginated list of exhibition templates.
/// </summary>
public sealed record class ListTemplatesQuery
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
        /// Gets an optional filter restricting results to a specific <see cref="TemplateComponentType"/>.
        /// </summary>
        public TemplateComponentType? ComponentType { get; init; }

        /// <summary>
        /// Gets optional search text matched against template display names.
        /// </summary>
        public string? SearchTerm { get; init; }
}

