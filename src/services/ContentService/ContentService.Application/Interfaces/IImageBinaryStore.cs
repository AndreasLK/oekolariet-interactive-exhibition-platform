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
}
