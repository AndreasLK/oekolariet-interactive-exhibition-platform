namespace ContentService.Application.Interfaces;

/// <summary>
/// Opens read-only binary streams for stored image payloads without loading megabytes into Large Object Heap arrays prematurely.
/// </summary>
/// <remarks>
/// Split from <see cref="IImageRepository"/> so HTTP streaming endpoints can read binary columns sequentially without pulling full entity graphs.
/// </remarks>
public interface IImageBinaryStore
{
        /// <summary>
        /// Opens a readable binary stream for the specified image identifier, or returns <see langword="null"/> if not found.
        /// </summary>
        /// <param name="imageId">The unique identifier of the image whose binary payload should be streamed.</param>
        /// <param name="cancellationToken">A token to observe while opening the underlying stream.</param>
        /// <returns>A readable <see cref="Stream"/> owned by the caller, or <see langword="null"/> if the binary payload is missing.</returns>
        public Task<Stream?> OpenReadAsync(Guid imageId, CancellationToken cancellationToken);
}

