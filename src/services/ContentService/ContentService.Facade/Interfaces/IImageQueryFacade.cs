using ContentService.Facade.Dtos;

namespace ContentService.Facade.Interfaces;

/// <summary>
/// CQS query facade contract for read-only exhibition image catalog, metadata, and stream operations.
/// </summary>
public interface IImageQueryFacade
{
        /// <summary>
        /// Lists active exhibition images matching pagination, search, and album filters.
        /// </summary>
        /// <param name="query">The image catalog query DTO.</param>
        /// <param name="cancellationToken">Propagates caller cancellation during lookup.</param>
        /// <returns>A paginated <see cref="PagedResultDto{T}"/> of <see cref="ImageMetadataDto"/>.</returns>
        public Task<PagedResultDto<ImageMetadataDto>> ListImagesAsync(
                ListImagesQueryDto query,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists immediate child album paths beneath an optional parent album.
        /// </summary>
        /// <param name="parentAlbumPath">Optional parent album path, or <see langword="null"/> for root albums.</param>
        /// <param name="cancellationToken">Propagates caller cancellation during lookup.</param>
        /// <returns>A sorted read-only list of immediate child album paths.</returns>
        public Task<IReadOnlyList<string>> ListAlbumsAsync(
                string? parentAlbumPath = null,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves metadata for an active exhibition image without transferring binary content.
        /// </summary>
        /// <param name="imageId">The unique image identifier.</param>
        /// <param name="cancellationToken">Propagates caller cancellation during lookup.</param>
        /// <returns>The <see cref="ImageMetadataDto"/> for the requested image.</returns>
        public Task<ImageMetadataDto> GetImageMetadataAsync(
                Guid imageId,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens the raw binary content stream for an active exhibition image.
        /// </summary>
        /// <param name="imageId">The unique image identifier.</param>
        /// <param name="cancellationToken">Propagates caller cancellation during stream opening.</param>
        /// <returns>A disposable <see cref="ImageStreamDto"/> owning the open stream.</returns>
        public Task<ImageStreamDto> GetImageContentAsync(
                Guid imageId,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens the lightweight preview image stream for an active exhibition image.
        /// </summary>
        /// <param name="imageId">The unique image identifier.</param>
        /// <param name="cancellationToken">Propagates caller cancellation during preview retrieval.</param>
        /// <returns>A disposable <see cref="ImageStreamDto"/> owning the open preview stream.</returns>
        public Task<ImageStreamDto> GetImagePreviewAsync(
                Guid imageId,
                CancellationToken cancellationToken = default);
}

