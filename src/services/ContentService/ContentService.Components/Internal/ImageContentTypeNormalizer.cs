namespace ContentService.Components.Internal;

/// <summary>
/// Normalizes browser-reported image MIME types to ContentService's canonical raster allowlist.
/// </summary>
internal static class ImageContentTypeNormalizer
{
        private const string MIME_PNG = "image/png";
        private const string MIME_JPEG = "image/jpeg";
        private const string MIME_WEBP = "image/webp";
        private const string LEGACY_JPG_MIME = "image/jpg";
        private const string GENERIC_BINARY_MIME = "application/octet-stream";

        /// <summary>
        /// Normalizes browser-reported MIME types to ContentService's canonical allowlist (<c>image/png</c>, <c>image/jpeg</c>, <c>image/webp</c>).
        /// </summary>
        /// <param name="rawContentType">The content type reported by <c>IBrowserFile.ContentType</c>.</param>
        /// <param name="fileName">The original file name used as fallback when browsers report blank or generic binary MIME types.</param>
        /// <returns>The normalized lowercase MIME type string.</returns>
        public static string Normalize(
                string? rawContentType,
                string fileName)
        {
                string trimmed = (rawContentType ?? string.Empty).Trim().ToLowerInvariant();

                // Some browsers (notably older Windows/Chromium setups) report non-standard "image/jpg" instead of RFC "image/jpeg".
                if (trimmed == LEGACY_JPG_MIME)
                {
                        return MIME_JPEG;
                }

                // Some Windows browsers emit an empty string or application/octet-stream for .webp files; infer from extension only as fallback.
                // Note: the server still checks the real magic bytes in ImageHeaderSignatureValidator, so extension fallback never bypasses security.
                if (!string.IsNullOrEmpty(value: trimmed) && trimmed != GENERIC_BINARY_MIME)
                {
                        return trimmed;
                }

                string extension = Path.GetExtension(path: fileName).ToLowerInvariant();
                return extension switch
                {
                        ".png" => MIME_PNG,
                        ".jpg" or ".jpeg" => MIME_JPEG,
                        ".webp" => MIME_WEBP,
                        _ => GENERIC_BINARY_MIME
                };
        }
}

