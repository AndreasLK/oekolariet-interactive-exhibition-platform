using ContentService.Facade.Dtos;

namespace ContentService.Facade.Interfaces;

/// <summary>
/// CQS query facade contract for read-only exhibition template catalog and item lookups.
/// </summary>
public interface ITemplateQueryFacade
{
        /// <summary>
        /// Lists exhibition templates matching pagination, component type, and search filters.
        /// </summary>
        /// <param name="query">The template catalog query DTO.</param>
        /// <param name="cancellationToken">Propagates caller cancellation during lookup.</param>
        /// <returns>A paginated <see cref="PagedResultDto{T}"/> of <see cref="TemplateMetadataDto"/>.</returns>
        public Task<PagedResultDto<TemplateMetadataDto>> ListTemplatesAsync(
                ListTemplatesQueryDto query,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a single exhibition template (including archived templates) by identifier.
        /// </summary>
        /// <param name="templateId">The unique template identifier.</param>
        /// <param name="cancellationToken">Propagates caller cancellation during lookup.</param>
        /// <returns>The requested <see cref="TemplateMetadataDto"/>.</returns>
        public Task<TemplateMetadataDto> GetTemplateAsync(
                Guid templateId,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks whether any local template (active or archived) references <paramref name="imageId"/>.
        /// </summary>
        /// <param name="imageId">The image identifier to check.</param>
        /// <param name="cancellationToken">Propagates caller cancellation during lookup.</param>
        /// <returns><see langword="true"/> if referenced locally; otherwise <see langword="false"/>.</returns>
        public Task<bool> IsImageReferencedByAnyTemplateAsync(
                Guid imageId,
                CancellationToken cancellationToken = default);
}

