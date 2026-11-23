using ContentService.Facade.Dtos;

namespace ContentService.Facade.Interfaces;

/// <summary>
/// CQS command facade contract for state-mutating exhibition image operations.
/// </summary>
public interface IImageCommandFacade
{
        /// <summary>
        /// Uploads and persists a new exhibition image.
        /// </summary>
        /// <param name="command">The image upload command DTO.</param>
        /// <param name="cancellationToken">Propagates caller cancellation during upload and persistence.</param>
        /// <returns>The persisted <see cref="ImageMetadataDto"/>.</returns>
        public Task<ImageMetadataDto> UploadImageAsync(
                UploadImageCommandDto command,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates searchable metadata on an active exhibition image with optimistic concurrency enforcement.
        /// </summary>
        /// <param name="command">The metadata update command DTO.</param>
        /// <param name="cancellationToken">Propagates caller cancellation during persistence.</param>
        /// <returns>The updated <see cref="ImageMetadataDto"/>.</returns>
        public Task<ImageMetadataDto> UpdateImageMetadataAsync(
                UpdateImageMetadataCommandDto command,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Soft-deletes an unreferenced exhibition image.
        /// </summary>
        /// <param name="command">The image deletion command DTO.</param>
        /// <param name="cancellationToken">Propagates caller cancellation during deletion.</param>
        /// <returns>A task representing the asynchronous deletion operation.</returns>
        public Task DeleteImageAsync(
                DeleteImageCommandDto command,
                CancellationToken cancellationToken = default);
}

