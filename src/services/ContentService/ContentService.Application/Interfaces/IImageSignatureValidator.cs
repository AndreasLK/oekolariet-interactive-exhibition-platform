namespace ContentService.Application.Interfaces;

/// <summary>
/// Verifies that the leading file-header bytes of an uploaded image match its declared raster MIME content type.
/// </summary>
public interface IImageSignatureValidator
{
        /// <summary>
        /// Gets the minimum number of leading file bytes needed to inspect all supported image format headers.
        /// </summary>
        public int RequiredHeaderByteCount { get; }

        /// <summary>
        /// Inspects leading file bytes to confirm they match the expected format header for <paramref name="declaredContentType"/>.
        /// </summary>
        /// <param name="headerBytes">
        /// A read-only slice of the leading bytes from the uploaded file, avoiding extra array copies.
        /// </param>
        /// <param name="declaredContentType">The MIME content type declared by the caller.</param>
        /// <returns><see langword="true"/> if the header bytes match the declared raster format; otherwise <see langword="false"/>.</returns>
        public bool IsValidSignature(ReadOnlySpan<byte> headerBytes, string declaredContentType);
}
