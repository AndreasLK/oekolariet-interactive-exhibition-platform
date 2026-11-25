using ContentService.Sdk.Models;

namespace ContentService.Sdk.Clients;

/// <summary>
/// Typed HTTP client contract for uploading, downloading, and resolving exhibition images from ContentService.
/// </summary>
public interface IContentServiceImageClient
{
        /// <summary>
        /// Uploads an image stream to ContentService and returns its persisted metadata.
        /// </summary>
        /// <param name="request">The upload stream, file name, MIME type, and optional metadata.</param>
        /// <param name="cancellationToken">A token to cancel the HTTP upload.</param>
        /// <returns>The persisted <see cref="ContentImageMetadata"/> returned by ContentService.</returns>
        Task<ContentImageMetadata> UploadImageAsync(
                UploadImageRequest request,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads the raw binary stream and HTTP headers for an active exhibition image.
        /// </summary>
        /// <param name="imageId">The unique identifier of the image to download.</param>
        /// <param name="cancellationToken">A token to cancel the HTTP request before headers arrive.</param>
        /// <returns>A disposable <see cref="ContentImageDownload"/> owning the payload stream and response.</returns>
        Task<ContentImageDownload> DownloadImageAsync(
                Guid imageId,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the metadata record for an active exhibition image.
        /// </summary>
        /// <param name="imageId">The unique identifier of the image.</param>
        /// <param name="cancellationToken">A token to cancel the HTTP request.</param>
        /// <returns>The <see cref="ContentImageMetadata"/> for the requested image.</returns>
        Task<ContentImageMetadata> GetImageMetadataAsync(
                Guid imageId,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Builds the absolute ContentService HTTP content URI for direct browser <c>&lt;img src&gt;</c> rendering.
        /// </summary>
        /// <param name="imageId">The unique identifier of the image.</param>
        /// <returns>The absolute URI pointing to <c>api/images/{imageId}/content</c>.</returns>
        Uri GetImageContentUri(Guid imageId);
}

