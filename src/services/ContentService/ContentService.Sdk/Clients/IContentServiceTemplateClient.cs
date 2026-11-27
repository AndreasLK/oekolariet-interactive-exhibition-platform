using ContentService.Sdk.Common;
using ContentService.Sdk.Models;

namespace ContentService.Sdk.Clients;

/// <summary>
/// Typed HTTP client contract for creating, listing, retrieving, updating, archiving,
/// and checking image references on ContentService exhibition templates.
/// </summary>
public interface IContentServiceTemplateClient
{
        /// <summary>
        /// Creates a new exhibition template in ContentService.
        /// </summary>
        /// <param name="command">The template name, target component type, schema JSON, and optional actor attribution.</param>
        /// <param name="cancellationToken">A token to cancel the HTTP request.</param>
        /// <returns>The newly persisted <see cref="ContentTemplateResponse"/>.</returns>
        public Task<ContentTemplateResponse> CreateTemplateAsync(
                ContentCreateTemplateRequest command,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists active exhibition templates using 1-based pagination and optional filtering.
        /// </summary>
        /// <param name="page">The 1-based page index to retrieve.</param>
        /// <param name="pageSize">The maximum number of items per page.</param>
        /// <param name="componentType">Optional component category filter.</param>
        /// <param name="searchTerm">Optional case-insensitive template name filter.</param>
        /// <param name="cancellationToken">A token to cancel the HTTP request.</param>
        /// <returns>A <see cref="ContentPagedResponse{T}"/> containing the requested page of templates.</returns>
        public Task<ContentPagedResponse<ContentTemplateResponse>> ListTemplatesAsync(
                int page = SdkConstants.DEFAULT_PAGE,
                int pageSize = SdkConstants.DEFAULT_PAGE_SIZE,
                ContentTemplateComponentType? componentType = null,
                string? searchTerm = null,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists active exhibition templates matching pagination, component type, and search filters.
        /// </summary>
        /// <param name="query">The template catalog query parameters.</param>
        /// <param name="cancellationToken">A token to cancel the HTTP request.</param>
        /// <returns>A <see cref="ContentPagedResponse{T}"/> containing matching template responses.</returns>
        public Task<ContentPagedResponse<ContentTemplateResponse>> ListTemplatesAsync(
                ContentTemplateQuery query,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the metadata and JSON schema for a specific exhibition template.
        /// </summary>
        /// <param name="templateId">The unique identifier of the template.</param>
        /// <param name="cancellationToken">A token to cancel the HTTP request.</param>
        /// <returns>The <see cref="ContentTemplateResponse"/> for the requested template.</returns>
        public Task<ContentTemplateResponse> GetTemplateAsync(
                Guid templateId,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing exhibition template with optimistic concurrency protection.
        /// </summary>
        /// <param name="templateId">The unique identifier of the template to update.</param>
        /// <param name="command">The updated template name, schema JSON, and expected concurrency stamp.</param>
        /// <param name="cancellationToken">A token to cancel the HTTP request.</param>
        /// <returns>The updated <see cref="ContentTemplateResponse"/> with an incremented schema version and rotated stamp.</returns>
        public Task<ContentTemplateResponse> UpdateTemplateAsync(
                Guid templateId,
                ContentUpdateTemplateRequest command,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Archives an active exhibition template in ContentService.
        /// </summary>
        /// <param name="templateId">The unique identifier of the template to archive.</param>
        /// <param name="actorId">Optional audit actor identifier overriding <c>ContentServiceClientOptions.DefaultActorId</c>.</param>
        /// <param name="cancellationToken">A token to cancel the HTTP request.</param>
        /// <returns>A task representing the asynchronous archival operation.</returns>
        public Task DeleteTemplateAsync(
                Guid templateId,
                string? actorId = null,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks whether any active template on the node references a given exhibition image.
        /// </summary>
        /// <param name="imageId">The unique identifier of the image to check.</param>
        /// <param name="cancellationToken">A token to cancel the HTTP request.</param>
        /// <returns>A <see cref="ContentImageTemplateReferenceResponse"/> indicating whether any active template references <paramref name="imageId"/>.</returns>
        public Task<ContentImageTemplateReferenceResponse> IsImageReferencedByTemplateAsync(
                Guid imageId,
                CancellationToken cancellationToken = default);
}
