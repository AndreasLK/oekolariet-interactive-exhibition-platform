namespace ContentService.Application.Interfaces;

/// <summary>
/// Generates lightweight resized image previews for fast gallery loading without transferring multi-megabyte originals.
/// </summary>
public interface IImagePreviewGenerator
{
        /// <summary>
        /// Attempts to decode <paramref name="sourceBytes"/> and produce a bounded-size preview payload.
        /// </summary>
        /// <param name="sourceBytes">The original uploaded image bytes.</param>
        /// <param name="cancellationToken">A token to cancel preview generation.</param>
        /// <returns>The generated <see cref="ImagePreviewPayload"/>, or <see langword="null"/> if the format cannot be resized.</returns>
        public Task<ImagePreviewPayload?> TryGeneratePreviewAsync(
                byte[] sourceBytes,
                CancellationToken cancellationToken);
}

