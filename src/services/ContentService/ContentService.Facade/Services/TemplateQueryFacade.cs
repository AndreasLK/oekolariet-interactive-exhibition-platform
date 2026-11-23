using ContentService.Application.Common;
using ContentService.Application.Models;
using ContentService.Application.UseCases.Templates;
using ContentService.Facade.Dtos;
using ContentService.Facade.Interfaces;
using ContentService.Facade.Mapping;

namespace ContentService.Facade.Services;

/// <summary>
/// Default <see cref="ITemplateQueryFacade"/> serving read-only template catalog and item queries.
/// </summary>
internal sealed class TemplateQueryFacade : ITemplateQueryFacade
{
        /// <summary>
        /// Application use case querying paginated template lists.
        /// </summary>
        private readonly ListTemplatesUseCase _listTemplatesUseCase;

        /// <summary>
        /// Application use case retrieving a single template by identifier.
        /// </summary>
        private readonly GetTemplateUseCase _getTemplateUseCase;

        /// <summary>
        /// Initializes a new instance of <see cref="TemplateQueryFacade"/>.
        /// </summary>
        /// <param name="listTemplatesUseCase">The list templates use case.</param>
        /// <param name="getTemplateUseCase">The get template use case.</param>
        public TemplateQueryFacade(
                ListTemplatesUseCase listTemplatesUseCase,
                GetTemplateUseCase getTemplateUseCase)
        {
                ArgumentNullException.ThrowIfNull(listTemplatesUseCase);
                ArgumentNullException.ThrowIfNull(getTemplateUseCase);

                this._listTemplatesUseCase = listTemplatesUseCase;
                this._getTemplateUseCase = getTemplateUseCase;
        }

        /// <inheritdoc />
        public async Task<PagedResultDto<TemplateMetadataDto>> ListTemplatesAsync(
                ListTemplatesQueryDto query,
                CancellationToken cancellationToken = default)
        {
                ArgumentNullException.ThrowIfNull(query);

                ListTemplatesQuery appQuery = new()
                {
                        Page = query.Page,
                        PageSize = query.PageSize,
                        ComponentType = query.ComponentType is not null
                                ? FacadeMapper.ToDomain(componentType: query.ComponentType.Value)
                                : null,
                        SearchTerm = query.SearchTerm
                };

                PagedResult<TemplateResult> result = await this._listTemplatesUseCase.ExecuteAsync(
                        query: appQuery,
                        cancellationToken: cancellationToken);

                return FacadeMapper.ToPagedDto(
                        source: result,
                        mapItem: FacadeMapper.ToDto);
        }

        /// <inheritdoc />
        public async Task<TemplateMetadataDto> GetTemplateAsync(
                Guid templateId,
                CancellationToken cancellationToken = default)
        {
                TemplateResult result = await this._getTemplateUseCase.ExecuteAsync(
                        templateId: templateId,
                        cancellationToken: cancellationToken);

                return FacadeMapper.ToDto(result: result);
        }
}

