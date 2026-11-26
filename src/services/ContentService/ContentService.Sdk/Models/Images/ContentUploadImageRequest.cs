namespace ContentService.Sdk.Models;

/// <summary>
/// Command payload required to upload an exhibition image stream to ContentService.
/// </summary>
public sealed record class ContentUploadImageRequest
{
        /// <summary>
        /// Gets the readable binary stream containing the image payload.
        /// </summary>
        public required Stream Content { get; init; }

        /// <summary>
        /// Gets the original file name (UTF-8 characters such as Danish æ/ø/å are preserved).
        /// </summary>
        public required string FileName { get; init; }

        /// <summary>
        /// Gets the MIME content type of the uploaded image (for example <c>image/png</c>).
        /// </summary>
        public required string ContentType { get; init; }

        /// <summary>
        /// Gets an optional accessibility alternative text / description for the image.
        /// </summary>
        public string? AltText { get; init; }

        /// <summary>
        /// Gets an optional recursive folder path (for example <c>animals/mammals</c>) where the image will be stored.
        /// </summary>
        public string? AlbumPath { get; init; }

        /// <summary>
        /// Gets optional comma-separated search tags to associate with the uploaded image.
        /// </summary>
        public string? Tags { get; init; }

        /// <summary>
        /// Gets an optional audit actor identifier overriding <see cref="Options.ContentServiceClientOptions.DefaultActorId"/>.
        /// </summary>
        public string? ActorId { get; init; }
}

