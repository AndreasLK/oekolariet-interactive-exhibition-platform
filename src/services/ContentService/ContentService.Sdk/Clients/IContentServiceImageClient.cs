using ContentService.Sdk.Common;
using ContentService.Sdk.Models;

namespace ContentService.Sdk.Clients;

/// <summary>
/// Typed HTTP client contract for listing, uploading, downloading, deleting, and resolving exhibition images from ContentService.
/// </summary>
public interface IContentServiceImageClient
{
        /// <summary>
        /// Lists active exhibition images using 1-based pagination and optional search filtering.
        /// </summary>
        /// <param name="page">The 1-based page index to retrieve.</param>
        /// <param name="pageSize">The maximum number of items per page.</param>
        /// <param name="searchTerm">Optional case-insensitive search filter across file name, alt text, and tags.</param>
        /// <param name="cancellationToken">A token to cancel the HTTP request.</param>
        /// <returns>A <see cref="ContentPagedResult{T}"/> containing the requested page of image metadata.</returns>
        public Task<ContentPagedResult<ContentImageMetadata>> ListImagesAsync(
                int page = SdkConstants.DEFAULT_PAGE,
                int pageSize = SdkConstants.DEFAULT_PAGE_SIZE,
                string? searchTerm = null,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists active exhibition images matching pagination, search text, and album hierarchy filters.
        /// </summary>
        /// <param name="query">The image catalog query parameters.</param>
        /// <param name="cancellationToken">A token to cancel the HTTP request.</param>
        /// <returns>A <see cref="ContentPagedResult{T}"/> containing matching image metadata.</returns>
        public Task<ContentPagedResult<ContentImageMetadata>> ListImagesAsync(
                ContentImageQuery query,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists distinct immediate child album paths beneath an optional parent album.
        /// </summary>
        /// <param name="parentPath">The optional parent album path, or <see langword="null"/> for root albums.</param>
        /// <param name="cancellationToken">A token to cancel the HTTP request.</param>
        /// <returns>A sorted read-only list of immediate child album paths.</returns>
        public Task<IReadOnlyList<string>> ListAlbumsAsync(
                string? parentPath = null,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates searchable description (<c>AltText</c>), tags, and album path on an active image.
        /// </summary>
        /// <param name="imageId">The unique identifier of the image to update.</param>
        /// <param name="request">The updated metadata fields and expected concurrency stamp.</param>
        /// <param name="cancellationToken">A token to cancel the HTTP request.</param>
        /// <returns>The updated <see cref="ContentImageMetadata"/> with a rotated concurrency stamp.</returns>
        public Task<ContentImageMetadata> UpdateImageMetadataAsync(
                Guid imageId,
                UpdateImageMetadataRequest request,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Uploads an image stream to ContentService and returns its persisted metadata.
        /// </summary>
        /// <param name="request">The upload stream, file name, MIME type, and optional metadata.</param>
        /// <param name="cancellationToken">A token to cancel the HTTP upload.</param>
        /// <returns>The persisted <see cref="ContentImageMetadata"/> returned by ContentService.</returns>
        public Task<ContentImageMetadata> UploadImageAsync(
                UploadImageRequest request,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads the raw binary stream and HTTP headers for an active exhibition image.
        /// </summary>
        /// <param name="imageId">The unique identifier of the image to download.</param>
        /// <param name="cancellationToken">A token to cancel the HTTP request before headers arrive.</param>
        /// <returns>A disposable <see cref="ContentImageDownload"/> owning the payload stream and response.</returns>
        public Task<ContentImageDownload> DownloadImageAsync(
                Guid imageId,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads the lightweight preview stream and HTTP headers for an active exhibition image.
        /// </summary>
        /// <param name="imageId">The unique identifier of the image whose preview should be downloaded.</param>
        /// <param name="cancellationToken">A token to cancel the HTTP request before headers arrive.</param>
        /// <returns>A disposable <see cref="ContentImageDownload"/> owning the preview stream and response.</returns>
        public Task<ContentImageDownload> DownloadImagePreviewAsync(
                Guid imageId,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the metadata record for an active exhibition image.
        /// </summary>
        /// <param name="imageId">The unique identifier of the image.</param>
        /// <param name="cancellationToken">A token to cancel the HTTP request.</param>
        /// <returns>The <see cref="ContentImageMetadata"/> for the requested image.</returns>
        public Task<ContentImageMetadata> GetImageMetadataAsync(
                Guid imageId,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Soft-deletes an unreferenced exhibition image in ContentService.
        /// </summary>
        /// <param name="imageId">The unique identifier of the image to delete.</param>
        /// <param name="actorId">Optional audit actor identifier overriding <c>ContentServiceClientOptions.DefaultActorId</c>.</param>
        /// <param name="cancellationToken">A token to cancel the HTTP request.</param>
        /// <returns>A task representing the asynchronous deletion operation.</returns>
        public Task DeleteImageAsync(
                Guid imageId,
                string? actorId = null,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Builds the absolute ContentService HTTP content URI for direct browser <c>&lt;img src&gt;</c> rendering.
        /// </summary>
        /// <param name="imageId">The unique identifier of the image.</param>
        /// <returns>The absolute URI pointing to <c>api/images/{imageId}/content</c>.</returns>
        public Uri GetImageContentUri(Guid imageId);

        /// <summary>
        /// Builds the absolute ContentService HTTP preview URI for fast gallery thumbnail rendering.
        /// </summary>
        /// <param name="imageId">The unique identifier of the image.</param>
        /// <returns>The absolute URI pointing to <c>api/images/{imageId}/preview</c>.</returns>
        public Uri GetImagePreviewUri(Guid imageId) => this.GetImageContentUri(imageId: imageId);
}

