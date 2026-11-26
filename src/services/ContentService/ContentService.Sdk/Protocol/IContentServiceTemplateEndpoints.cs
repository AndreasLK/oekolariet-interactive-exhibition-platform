using ContentService.Sdk.Common;
using ContentService.Sdk.Models;
using Refit;

namespace ContentService.Sdk.Protocol;

/// <summary>
/// Internal Refit HTTP contract for <c>ContentService.Api</c> template endpoints.
/// </summary>
/// <remarks>
/// Kept <see langword="internal"/> behind <c>ContentServiceTemplateClient</c> so consumers interact with
/// normalized exceptions and options-driven API versioning rather than raw Refit proxies.
/// </remarks>
internal interface IContentServiceTemplateEndpoints
{
        /// <summary>
        /// Creates a new exhibition template.
        /// </summary>
        /// <param name="apiVersion">API version route segment.</param>
        /// <param name="command">Template creation payload.</param>
        /// <param name="actorId">Optional audit actor identifier sent via <c>X-Actor-Id</c>.</param>
        /// <param name="cancellationToken">Token to cancel the HTTP request.</param>
        /// <returns>The created template.</returns>
        [Post("/api/{apiVersion}/templates")]
        public Task<ContentTemplateResponse> CreateTemplateAsync(
                string apiVersion,
                [Body] ContentCreateTemplateCommand command,
                [Header(SdkConstants.ACTOR_HEADER_NAME)] string? actorId,
                CancellationToken cancellationToken);

        /// <summary>
        /// Lists active exhibition templates matching pagination, component type, and search filters.
        /// </summary>
        /// <param name="apiVersion">API version route segment.</param>
        /// <param name="page">1-based page number.</param>
        /// <param name="pageSize">Maximum items per page.</param>
        /// <param name="componentType">Optional component type filter.</param>
        /// <param name="searchTerm">Optional case-insensitive search filter.</param>
        /// <param name="cancellationToken">Token to cancel the HTTP request.</param>
        /// <returns>A paged list of matching templates.</returns>
        [Get("/api/{apiVersion}/templates")]
        public Task<ContentPagedResponse<ContentTemplateResponse>> ListTemplatesAsync(
                string apiVersion,
                [Query] int page,
                [Query] int pageSize,
                [Query] ContentTemplateComponentType? componentType,
                [Query] string? searchTerm,
                CancellationToken cancellationToken);

        /// <summary>
        /// Retrieves a single exhibition template by identifier.
        /// </summary>
        /// <param name="apiVersion">API version route segment.</param>
        /// <param name="templateId">Template identifier.</param>
        /// <param name="cancellationToken">Token to cancel the HTTP request.</param>
        /// <returns>The requested template.</returns>
        [Get("/api/{apiVersion}/templates/{templateId}")]
        public Task<ContentTemplateResponse> GetTemplateAsync(
                string apiVersion,
                Guid templateId,
                CancellationToken cancellationToken);

        /// <summary>
        /// Updates an existing exhibition template with optimistic concurrency protection.
        /// </summary>
        /// <param name="apiVersion">API version route segment.</param>
        /// <param name="templateId">Template identifier.</param>
        /// <param name="command">Updated template payload including expected row version.</param>
        /// <param name="actorId">Optional audit actor identifier sent via <c>X-Actor-Id</c>.</param>
        /// <param name="cancellationToken">Token to cancel the HTTP request.</param>
        /// <returns>The updated template.</returns>
        [Put("/api/{apiVersion}/templates/{templateId}")]
        public Task<ContentTemplateResponse> UpdateTemplateAsync(
                string apiVersion,
                Guid templateId,
                [Body] ContentUpdateTemplateCommand command,
                [Header(SdkConstants.ACTOR_HEADER_NAME)] string? actorId,
                CancellationToken cancellationToken);

        /// <summary>
        /// Archives an active exhibition template.
        /// </summary>
        /// <param name="apiVersion">API version route segment.</param>
        /// <param name="templateId">Template identifier.</param>
        /// <param name="actorId">Optional audit actor identifier sent via <c>X-Actor-Id</c>.</param>
        /// <param name="cancellationToken">Token to cancel the HTTP request.</param>
        [Delete("/api/{apiVersion}/templates/{templateId}")]
        public Task DeleteTemplateAsync(
                string apiVersion,
                Guid templateId,
                [Header(SdkConstants.ACTOR_HEADER_NAME)] string? actorId,
                CancellationToken cancellationToken);

        /// <summary>
        /// Checks whether any active template references the specified image.
        /// </summary>
        /// <param name="apiVersion">API version route segment.</param>
        /// <param name="imageId">Image identifier to check.</param>
        /// <param name="cancellationToken">Token to cancel the HTTP request.</param>
        /// <returns>Reference status indicating whether deletion should be blocked.</returns>
        [Get("/api/{apiVersion}/templates/image-references/{imageId}")]
        public Task<ContentImageTemplateReferenceResponse> IsImageReferencedByTemplateAsync(
                string apiVersion,
                Guid imageId,
                CancellationToken cancellationToken);
}

