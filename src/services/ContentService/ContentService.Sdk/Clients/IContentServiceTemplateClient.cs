using ContentService.Sdk.Models;

namespace ContentService.Sdk.Clients;

/// <summary>
/// Typed HTTP client contract for listing, retrieving, creating, updating, and archiving exhibition templates in ContentService.
/// </summary>
public interface IContentServiceTemplateClient
{
        /// <summary>
        /// Lists active exhibition templates matching pagination, component type, and search filters.
        /// </summary>
        /// <param name="query">The template catalog query parameters.</param>
        /// <param name="cancellationToken">A token to cancel the HTTP request.</param>
        /// <returns>A <see cref="ContentPagedResult{T}"/> containing matching template records.</returns>
        public Task<ContentPagedResult<ContentTemplateMetadata>> ListTemplatesAsync(
                ContentTemplateQuery? query = null,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a single exhibition template by its unique identifier.
        /// </summary>
        /// <param name="templateId">The unique identifier of the template.</param>
        /// <param name="cancellationToken">A token to cancel the HTTP request.</param>
        /// <returns>The matching <see cref="ContentTemplateMetadata"/>.</returns>
        public Task<ContentTemplateMetadata> GetTemplateAsync(
                Guid templateId,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new exhibition layout template in ContentService.
        /// </summary>
        /// <param name="request">The template creation payload.</param>
        /// <param name="cancellationToken">A token to cancel the HTTP request.</param>
        /// <returns>The persisted <see cref="ContentTemplateMetadata"/>.</returns>
        public Task<ContentTemplateMetadata> CreateTemplateAsync(
                CreateContentTemplateRequest request,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing exhibition template while enforcing optimistic concurrency.
        /// </summary>
        /// <param name="templateId">The unique identifier of the template to update.</param>
        /// <param name="request">The updated template payload and expected concurrency stamp.</param>
        /// <param name="cancellationToken">A token to cancel the HTTP request.</param>
        /// <returns>The updated <see cref="ContentTemplateMetadata"/>.</returns>
        public Task<ContentTemplateMetadata> UpdateTemplateAsync(
                Guid templateId,
                UpdateContentTemplateRequest request,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Archives (soft-deletes) an exhibition template in ContentService.
        /// </summary>
        /// <param name="templateId">The unique identifier of the template to archive.</param>
        /// <param name="actorId">Optional audit actor identifier overriding <c>ContentServiceClientOptions.DefaultActorId</c>.</param>
        /// <param name="cancellationToken">A token to cancel the HTTP request.</param>
        /// <returns>A task representing the asynchronous archive operation.</returns>
        public Task DeleteTemplateAsync(
                Guid templateId,
                string? actorId = null,
                CancellationToken cancellationToken = default);
}
