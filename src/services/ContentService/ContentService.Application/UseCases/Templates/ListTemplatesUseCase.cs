using ContentService.Application.Interfaces;
using ContentService.Application.Common;
using ContentService.Application.Models;
using ContentService.Domain.Entities;
using ContentService.Domain.Exceptions;

namespace ContentService.Application.UseCases.Templates;

/// <summary>
/// Retrieves a bounded, paginated slice of exhibition templates optionally filtered by component type or name search text.
/// </summary>
public sealed class ListTemplatesUseCase
{
        /// <summary>
        /// Template aggregate repository.
        /// </summary>
        private readonly ITemplateRepository _templateRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="ListTemplatesUseCase"/> class.
        /// </summary>
        /// <param name="templateRepository">The template persistence repository.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="templateRepository"/> is <see langword="null"/>.</exception>
        public ListTemplatesUseCase(ITemplateRepository templateRepository)
        {
                ArgumentNullException.ThrowIfNull(templateRepository);
                this._templateRepository = templateRepository;
        }

        /// <summary>
        /// Executes a paginated query for exhibition templates after validating filters and clamping pagination bounds.
        /// </summary>
        /// <param name="query">The pagination, component type, and search criteria.</param>
        /// <param name="cancellationToken">A token to observe while querying templates.</param>
        /// <returns>A <see cref="PagedResult{T}"/> of projected <see cref="TemplateResult"/> items.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
        /// <exception cref="DomainValidationException">Thrown when <see cref="ListTemplatesQuery.ComponentType"/> is an undefined enum value.</exception>
        public async Task<PagedResult<TemplateResult>> ExecuteAsync(
                ListTemplatesQuery query,
                CancellationToken cancellationToken)
        {
                ArgumentNullException.ThrowIfNull(query);

                if (query.ComponentType.HasValue && !Enum.IsDefined(query.ComponentType.Value))
                {
                        throw new DomainValidationException(
                                $"Template component type '{query.ComponentType.Value}' is not recognized.");
                }

                // Clamp invalid pagination values gracefully and cap Page to prevent (page - 1) * pageSize int overflow downstream.
                int clampedPageSize = Math.Clamp(
                        value: query.PageSize,
                        min: ApplicationConstants.MIN_PAGE_SIZE,
                        max: ApplicationConstants.MAX_PAGE_SIZE);
                int maxValidPage = (int)Math.Min(val1: int.MaxValue, val2: ((long)int.MaxValue / clampedPageSize) + 1L);
                int clampedPage = Math.Clamp(
                        value: query.Page,
                        min: ApplicationConstants.DEFAULT_PAGE,
                        max: maxValidPage);

                PagedResult<TemplateEntity> pagedEntities = await this._templateRepository.ListAsync(
                        page: clampedPage,
                        pageSize: clampedPageSize,
                        componentType: query.ComponentType,
                        searchTerm: query.SearchTerm,
                        cancellationToken: cancellationToken);

                IReadOnlyList<TemplateResult> projectedItems = pagedEntities.Items
                        .Select(ContentResultMapper.ToTemplateResult)
                        .ToArray();

                return new PagedResult<TemplateResult>
                {
                        Items = projectedItems,
                        Page = pagedEntities.Page,
                        PageSize = pagedEntities.PageSize,
                        TotalCount = pagedEntities.TotalCount
                };
        }
}

