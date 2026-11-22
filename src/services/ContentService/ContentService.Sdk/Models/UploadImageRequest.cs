namespace ContentService.Sdk.Models;

/// <summary>
/// Parameters required to upload an exhibition image to ContentService.
/// </summary>
public sealed record class UploadImageRequest
{
        /// <summary>
        /// Gets the readable binary stream containing the image payload.
        /// </summary>
        public required Stream Content { get; init; }

        /// <summary>
        /// Gets the original file name including extension (supports UTF-8 characters such as Danish letters).
        /// </summary>
        public required string FileName { get; init; }

        /// <summary>
        /// Gets the declared MIME content type (e.g., <c>image/png</c>, <c>image/jpeg</c>, or <c>image/webp</c>).
        /// </summary>
        public required string ContentType { get; init; }

        /// <summary>
        /// Gets optional accessibility alternative text / description persisted alongside the image metadata.
        /// </summary>
        public string? AltText { get; init; }

        /// <summary>
        /// Gets an optional recursive album path (e.g., <c>Madaffald/Frugt</c>) where the image will be filed.
        /// </summary>
        public string? AlbumPath { get; init; }

        /// <summary>
        /// Gets optional comma-separated search tags persisted alongside the image metadata.
        /// </summary>
        public string? Tags { get; init; }

        /// <summary>
        /// Gets an optional audit actor identifier overriding <see cref="Options.ContentServiceClientOptions.DefaultActorId"/> for this upload.
        /// </summary>
        public string? ActorId { get; init; }
}

