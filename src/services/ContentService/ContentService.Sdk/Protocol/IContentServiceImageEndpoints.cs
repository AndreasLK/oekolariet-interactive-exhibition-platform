using ContentService.Sdk.Common;
using ContentService.Sdk.Models;
using Refit;

namespace ContentService.Sdk.Protocol;

/// <summary>
/// Internal Refit HTTP contract for <c>ContentService.Api</c> image endpoints.
/// </summary>
/// <remarks>
/// Non-standard design: Instead of exposing Refit interfaces directly in DI, we keep them <see langword="internal"/>
/// behind <c>ContentServiceImageClient</c>.
/// <para>
/// What we gain: Callers are shielded from Refit types (<c>ApiException</c>, <c>MultipartFormDataContent</c>),
/// get normalized <c>ContentServiceApiException</c> errors, and retain stream-safe upload/download helpers.
/// What we lose: A thin wrapper layer of indirection when adding new endpoints.
/// </para>
/// </remarks>
internal interface IContentServiceImageEndpoints
{
        /// <summary>
        /// Lists active exhibition images matching pagination, search, and album filters.
        /// </summary>
        /// <param name="apiVersion">API version route segment (for example <c>v1</c>).</param>
        /// <param name="page">1-based page number.</param>
        /// <param name="pageSize">Maximum items per page.</param>
        /// <param name="searchTerm">Optional case-insensitive search filter.</param>
        /// <param name="albumPath">Optional album path filter.</param>
        /// <param name="includeSubalbums">Optional string-encoded boolean (<c>"true"</c>/<c>"false"</c>) controlling recursive album matching.</param>
        /// <param name="cancellationToken">Token to cancel the HTTP request.</param>
        /// <returns>A paged list of matching image metadata records.</returns>
        [Get("/api/{apiVersion}/images")]
        public Task<ContentPagedResponse<ContentImageResponse>> ListImagesAsync(
                string apiVersion,
                [Query] int page,
                [Query] int pageSize,
                [Query] string? searchTerm,
                [Query] string? albumPath,
                [Query] string? includeSubalbums,
                CancellationToken cancellationToken);

        /// <summary>
        /// Lists distinct immediate child album paths beneath an optional parent album.
        /// </summary>
        /// <param name="apiVersion">API version route segment.</param>
        /// <param name="parentPath">Optional parent album path; <see langword="null"/> lists root albums.</param>
        /// <param name="cancellationToken">Token to cancel the HTTP request.</param>
        /// <returns>Sorted child album paths.</returns>
        [Get("/api/{apiVersion}/images/albums")]
        public Task<IReadOnlyList<string>> ListAlbumsAsync(
                string apiVersion,
                [Query] string? parentPath,
                CancellationToken cancellationToken);

        /// <summary>
        /// Retrieves image responses for a batch of active image identifiers.
        /// </summary>
        /// <param name="apiVersion">API version route segment.</param>
        /// <param name="imageIds">Image GUIDs to resolve in a single round-trip.</param>
        /// <param name="cancellationToken">Token to cancel the HTTP request.</param>
        /// <returns>Metadata for all found active images.</returns>
        [Post("/api/{apiVersion}/images/batch-metadata")]
        public Task<IReadOnlyList<ContentImageResponse>> GetImagesMetadataBatchAsync(
                string apiVersion,
                [Body] IReadOnlyCollection<Guid> imageIds,
                CancellationToken cancellationToken);

        /// <summary>
        /// Updates searchable metadata on an active image.
        /// </summary>
        /// <param name="apiVersion">API version route segment.</param>
        /// <param name="imageIdentifier">Image GUID or shard-aware <c>{node}_{guid}</c> identifier.</param>
        /// <param name="command">Metadata patch payload.</param>
        /// <param name="actorId">Optional audit actor identifier sent via <c>X-Actor-Id</c>.</param>
        /// <param name="cancellationToken">Token to cancel the HTTP request.</param>
        /// <returns>The updated image metadata.</returns>
        [Patch("/api/{apiVersion}/images/{imageIdentifier}/metadata")]
        public Task<ContentImageResponse> UpdateImageMetadataAsync(
                string apiVersion,
                string imageIdentifier,
                [Body] ContentUpdateImageMetadataCommand command,
                [Header(SdkConstants.ACTOR_HEADER_NAME)] string? actorId,
                CancellationToken cancellationToken);

        /// <summary>
        /// Uploads a pre-built multipart form-data image payload.
        /// </summary>
        /// <param name="apiVersion">API version route segment.</param>
        /// <param name="content">Pre-built multipart payload from <see cref="ImageUploadMultipartContentFactory"/>.</param>
        /// <param name="actorId">Optional audit actor identifier sent via <c>X-Actor-Id</c>.</param>
        /// <param name="cancellationToken">Token to cancel the HTTP request.</param>
        /// <returns>The created image metadata.</returns>
        [Post("/api/{apiVersion}/images")]
        public Task<ContentImageResponse> UploadImageAsync(
                string apiVersion,
                [Body] MultipartFormDataContent content,
                [Header(SdkConstants.ACTOR_HEADER_NAME)] string? actorId,
                CancellationToken cancellationToken);

        /// <summary>
        /// Streams the raw binary image content response.
        /// </summary>
        /// <param name="apiVersion">API version route segment.</param>
        /// <param name="imageIdentifier">Image GUID or shard-aware <c>{node}_{guid}</c> identifier.</param>
        /// <param name="cancellationToken">Token to cancel the HTTP request.</param>
        /// <returns>The raw <see cref="HttpResponseMessage"/> so callers own stream disposal.</returns>
        [Get("/api/{apiVersion}/images/{imageIdentifier}/content")]
        public Task<HttpResponseMessage> DownloadImageAsync(
                string apiVersion,
                string imageIdentifier,
                CancellationToken cancellationToken);

        /// <summary>
        /// Streams the lightweight preview image response.
        /// </summary>
        /// <param name="apiVersion">API version route segment.</param>
        /// <param name="imageIdentifier">Image GUID or shard-aware <c>{node}_{guid}</c> identifier.</param>
        /// <param name="cancellationToken">Token to cancel the HTTP request.</param>
        /// <returns>The raw <see cref="HttpResponseMessage"/> so callers own stream disposal.</returns>
        [Get("/api/{apiVersion}/images/{imageIdentifier}/preview")]
        public Task<HttpResponseMessage> DownloadImagePreviewAsync(
                string apiVersion,
                string imageIdentifier,
                CancellationToken cancellationToken);

        /// <summary>
        /// Retrieves metadata for a single active image.
        /// </summary>
        /// <param name="apiVersion">API version route segment.</param>
        /// <param name="imageIdentifier">Image GUID or shard-aware <c>{node}_{guid}</c> identifier.</param>
        /// <param name="cancellationToken">Token to cancel the HTTP request.</param>
        /// <returns>The image metadata.</returns>
        [Get("/api/{apiVersion}/images/{imageIdentifier}")]
        public Task<ContentImageResponse> GetImageMetadataAsync(
                string apiVersion,
                string imageIdentifier,
                CancellationToken cancellationToken);

        /// <summary>
        /// Soft-deletes an unreferenced exhibition image.
        /// </summary>
        /// <param name="apiVersion">API version route segment.</param>
        /// <param name="imageIdentifier">Image GUID or shard-aware <c>{node}_{guid}</c> identifier.</param>
        /// <param name="actorId">Optional audit actor identifier sent via <c>X-Actor-Id</c>.</param>
        /// <param name="cancellationToken">Token to cancel the HTTP request.</param>
        [Delete("/api/{apiVersion}/images/{imageIdentifier}")]
        public Task DeleteImageAsync(
                string apiVersion,
                string imageIdentifier,
                [Header(SdkConstants.ACTOR_HEADER_NAME)] string? actorId,
                CancellationToken cancellationToken);
}

