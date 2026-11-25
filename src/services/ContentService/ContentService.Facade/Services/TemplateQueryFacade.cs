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
        /// Application use case checking whether any local template references an image ID.
        /// </summary>
        private readonly CheckImageTemplateReferenceUseCase _checkImageTemplateReferenceUseCase;

        /// <summary>
        /// Initializes a new instance of <see cref="TemplateQueryFacade"/>.
        /// </summary>
        /// <param name="listTemplatesUseCase">The list templates use case.</param>
        /// <param name="getTemplateUseCase">The get template use case.</param>
        /// <param name="checkImageTemplateReferenceUseCase">The image template reference check use case.</param>
        public TemplateQueryFacade(
                ListTemplatesUseCase listTemplatesUseCase,
                GetTemplateUseCase getTemplateUseCase,
                CheckImageTemplateReferenceUseCase checkImageTemplateReferenceUseCase)
        {
                ArgumentNullException.ThrowIfNull(listTemplatesUseCase);
                ArgumentNullException.ThrowIfNull(getTemplateUseCase);
                ArgumentNullException.ThrowIfNull(checkImageTemplateReferenceUseCase);

                this._listTemplatesUseCase = listTemplatesUseCase;
                this._getTemplateUseCase = getTemplateUseCase;
                this._checkImageTemplateReferenceUseCase = checkImageTemplateReferenceUseCase;
        }

        /// <inheritdoc />
        public async Task<PagedResultDto<TemplateMetadataDto>> ListTemplatesAsync(
                ListTemplatesQueryDto query,
                CancellationToken cancellationToken = default)
        {
                ArgumentNullException.ThrowIfNull(query);

                // Translate the Facade DTO query into the Application-layer query model.
                ListTemplatesQuery appQuery = new()
                {
                        Page = query.Page,
                        PageSize = query.PageSize,
                        ComponentType = query.ComponentType is not null
                                ? FacadeMapper.ToDomain(componentType: query.ComponentType.Value)
                                : null,
                        SearchTerm = query.SearchTerm
                };

                // Execute the paged template query and map results back to Facade DTOs.
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
                // Fetch the single template from the Application layer and map to DTO.
                TemplateResult result = await this._getTemplateUseCase.ExecuteAsync(
                        templateId: templateId,
                        cancellationToken: cancellationToken);

                return FacadeMapper.ToDto(result: result);
        }

        /// <inheritdoc />
        public Task<bool> IsImageReferencedByAnyTemplateAsync(
                Guid imageId,
                CancellationToken cancellationToken = default)
        {
                // Delegate local template-reference lookup for peer-node delete-protection checks.
                return this._checkImageTemplateReferenceUseCase.ExecuteAsync(
                        imageId: imageId,
                        cancellationToken: cancellationToken);
        }
}

