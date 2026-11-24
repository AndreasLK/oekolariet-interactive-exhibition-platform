namespace ContentService.Application.Interfaces;

/// <summary>
/// Opens read-only binary streams for stored image payloads separately from metadata queries.
/// </summary>
/// <remarks>
/// Split from <see cref="IImageRepository"/> so catalog queries never pull image byte arrays when only metadata is needed.
/// </remarks>
public interface IImageBinaryStore
{
        /// <summary>
        /// Opens a readable binary stream for the specified image identifier, or returns <see langword="null"/> if not found.
        /// </summary>
        /// <param name="imageId">The unique identifier of the image whose binary payload should be read.</param>
        /// <param name="cancellationToken">A token to observe while loading the binary payload.</param>
        /// <returns>A readable <see cref="Stream"/> owned by the caller, or <see langword="null"/> if the binary payload is missing.</returns>
        public Task<Stream?> OpenReadAsync(Guid imageId, CancellationToken cancellationToken);

        /// <summary>
        /// Opens a readable binary stream for the saved lightweight preview of the specified image, or returns <see langword="null"/> if no preview is saved yet.
        /// </summary>
        /// <param name="imageId">The unique identifier of the image whose preview should be read.</param>
        /// <param name="cancellationToken">A token to observe while loading the preview payload.</param>
        /// <returns>An <see cref="ImagePreviewStreamResult"/> owned by the caller, or <see langword="null"/> if no preview is stored.</returns>
        public Task<ImagePreviewStreamResult?> OpenPreviewReadAsync(Guid imageId, CancellationToken cancellationToken) =>
                Task.FromResult<ImagePreviewStreamResult?>(result: null);

        /// <summary>
        /// Persists a generated lightweight preview payload for an existing image binary row.
        /// </summary>
        /// <param name="imageId">The unique identifier of the owning image.</param>
        /// <param name="previewBytes">The encoded preview bytes.</param>
        /// <param name="contentType">The MIME content type of the preview bytes.</param>
        /// <param name="cancellationToken">A token to observe while saving the preview payload.</param>
        /// <returns>A task representing the asynchronous save operation.</returns>
        public Task SavePreviewAsync(
                Guid imageId,
                byte[] previewBytes,
                string contentType,
                CancellationToken cancellationToken) =>
                Task.CompletedTask;
}
