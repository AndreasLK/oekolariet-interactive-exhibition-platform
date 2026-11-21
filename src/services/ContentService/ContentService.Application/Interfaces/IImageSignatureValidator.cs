namespace ContentService.Application.Interfaces;

/// <summary>
/// Verifies that the leading magic bytes of an uploaded image payload match its declared raster MIME content type.
/// </summary>
public interface IImageSignatureValidator
{
        /// <summary>
        /// Inspects leading payload bytes to confirm they match the expected magic-number signature for <paramref name="declaredContentType"/>.
        /// </summary>
        /// <param name="headerBytes">
        /// A read-only slice of the leading bytes from the uploaded payload, avoiding extra buffer copies during stream inspection.
        /// </param>
        /// <param name="declaredContentType">The MIME content type declared by the caller.</param>
        /// <returns><see langword="true"/> if the header signature matches the declared raster format; otherwise <see langword="false"/>.</returns>
        public bool IsValidSignature(ReadOnlySpan<byte> headerBytes, string declaredContentType);
}

