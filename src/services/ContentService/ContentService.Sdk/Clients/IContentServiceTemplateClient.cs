using ContentService.Sdk.Common;
using ContentService.Sdk.Models;

namespace ContentService.Sdk.Clients;

/// <summary>
/// Typed client contract for managing exhibition templates via <c>ContentService.Api</c>.
/// </summary>
public interface IContentServiceTemplateClient
{
        /// <summary>
        /// Lists active exhibition templates with pagination and optional component-type or name filtering.
        /// </summary>
        /// <param name="page">1-based page number (defaults to 1).</param>
        /// <param name="pageSize">Maximum items per page (defaults to 25).</param>
        /// <param name="componentType">Optional component type filter.</param>
        /// <param name="searchTerm">Optional search text matched against template names.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A paginated catalog page of template metadata.</returns>
        public Task<ContentPagedResult<ContentTemplateMetadata>> ListTemplatesAsync(
                int page = SdkConstants.DEFAULT_PAGE,
                int pageSize = SdkConstants.DEFAULT_PAGE_SIZE,
                ContentTemplateComponentType? componentType = null,
                string? searchTerm = null,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists active exhibition templates using structured filter and pagination criteria.
        /// </summary>
        /// <param name="query">Structured query criteria including component type and search filters.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A paginated catalog page of template metadata.</returns>
        public Task<ContentPagedResult<ContentTemplateMetadata>> ListTemplatesAsync(
                ContentTemplateQuery query,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves metadata and schema JSON for a single template by identifier.
        /// </summary>
        /// <param name="templateId">The unique template identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The template metadata and schema payload.</returns>
        public Task<ContentTemplateMetadata> GetTemplateAsync(
                Guid templateId,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new versioned exhibition template.
        /// </summary>
        /// <param name="request">Template creation payload including name, component type, and schema JSON.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created template metadata.</returns>
        public Task<ContentTemplateMetadata> CreateTemplateAsync(
                CreateContentTemplateRequest request,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing template's name and schema JSON using optimistic concurrency control.
        /// </summary>
        /// <param name="templateId">The unique template identifier.</param>
        /// <param name="request">Updated template metadata and expected concurrency stamp.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The updated template metadata with its rotated concurrency stamp.</returns>
        public Task<ContentTemplateMetadata> UpdateTemplateAsync(
                Guid templateId,
                UpdateContentTemplateRequest request,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Archives an existing exhibition template.
        /// </summary>
        /// <param name="templateId">The unique template identifier.</param>
        /// <param name="actorId">Optional actor identifier overriding configured default attribution.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public Task DeleteTemplateAsync(
                Guid templateId,
                string? actorId = null,
                CancellationToken cancellationToken = default);
}
