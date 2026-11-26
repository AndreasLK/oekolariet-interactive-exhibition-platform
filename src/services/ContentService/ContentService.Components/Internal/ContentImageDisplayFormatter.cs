using ContentService.Sdk.Models;

namespace ContentService.Components.Internal;

/// <summary>
/// Formats human-readable file sizes and gallery tile subtitles for image metadata cards.
/// </summary>
internal static class ContentImageDisplayFormatter
{
        private const long BYTES_PER_KIBIBYTE = 1024L;
        private const long BYTES_PER_MEBIBYTE = BYTES_PER_KIBIBYTE * BYTES_PER_KIBIBYTE;

        /// <summary>
        /// Formats a binary byte length into a concise human-readable KiB/MiB string.
        /// </summary>
        /// <param name="sizeBytes">The payload size in bytes.</param>
        /// <returns>A formatted byte, KB, or MB string.</returns>
        public static string FormatByteSize(long sizeBytes)
        {
                if (sizeBytes < BYTES_PER_KIBIBYTE)
                {
                        return $"{sizeBytes} B";
                }

                if (sizeBytes < BYTES_PER_MEBIBYTE)
                {
                        double kibibytes = (double)sizeBytes / BYTES_PER_KIBIBYTE;
                        return $"{kibibytes:0.#} KB";
                }

                double mebibytes = (double)sizeBytes / BYTES_PER_MEBIBYTE;
                return $"{mebibytes:0.##} MB";
        }

        /// <summary>
        /// Formats the secondary subtitle line rendered beneath each <c>MediaTile</c> in the gallery grid.
        /// </summary>
        /// <param name="image">The image metadata record.</param>
        /// <returns>A compact summary displaying file size, upload date, and optional album badge.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
        public static string FormatTileSubtitle(ContentImageResponse image)
        {
                ArgumentNullException.ThrowIfNull(argument: image);
                string formattedSize = FormatByteSize(sizeBytes: image.SizeBytes);
                string baseSummary = $"{formattedSize} • {image.CreatedAt.ToLocalTime():yyyy-MM-dd}";

                return string.IsNullOrWhiteSpace(value: image.AlbumPath)
                        ? baseSummary
                        : $"{baseSummary} • {image.AlbumPath}";
        }
}

