namespace ContentService.Application.Models;

/// <summary>
/// Command payload for uploading and validating a new raster exhibition image.
/// </summary>
public sealed record class UploadImageCommand
{
        /// <summary>
        /// Gets the original uploaded file name (validated against path traversal and length rules).
        /// </summary>
        public required string FileName { get; init; }

        /// <summary>
        /// Gets the declared MIME content type (must match the file's leading header bytes).
        /// </summary>
        public required string ContentType { get; init; }

        /// <summary>
        /// Gets the declared byte length reported by the transport caller before streaming.
        /// </summary>
        public required long DeclaredLengthBytes { get; init; }

        /// <summary>
        /// Gets the readable binary stream containing the uploaded image bytes.
        /// </summary>
        public required Stream Content { get; init; }

        /// <summary>
        /// Gets the identifier of the user or service uploading the image for audit logging.
        /// </summary>
        public required string ActorId { get; init; }

        /// <summary>
        /// Gets optional accessibility alternative text describing the image.
        /// </summary>
        public string? AltText { get; init; }

        /// <summary>
        /// Gets optional normalized recursive album path (e.g., <c>Madaffald/Frugt</c>).
        /// </summary>
        public string? AlbumPath { get; init; }

        /// <summary>
        /// Gets optional comma-separated search tags.
        /// </summary>
        public string? Tags { get; init; }
}

