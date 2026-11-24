namespace ContentService.Facade.Dtos;

/// <summary>
/// Command DTO for uploading a new exhibition image through the facade boundary.
/// </summary>
public sealed record class UploadImageCommandDto
{
        /// <summary>
        /// Gets the original file name supplied by the caller.
        /// </summary>
        public required string FileName { get; init; }

        /// <summary>
        /// Gets the normalized MIME media type declared by the caller.
        /// </summary>
        public required string ContentType { get; init; }

        /// <summary>
        /// Gets the declared payload length in bytes before streaming begins.
        /// </summary>
        public required long DeclaredLengthBytes { get; init; }

        /// <summary>
        /// Gets the readable stream containing the uploaded image bytes.
        /// </summary>
        public required Stream Content { get; init; }

        /// <summary>
        /// Gets the trusted actor identifier for audit attribution.
        /// </summary>
        public required string ActorId { get; init; }

        /// <summary>
        /// Gets optional accessibility alternative text / description.
        /// </summary>
        public string? AltText { get; init; }

        /// <summary>
        /// Gets optional recursive album path.
        /// </summary>
        public string? AlbumPath { get; init; }

        /// <summary>
        /// Gets optional comma-separated search tags.
        /// </summary>
        public string? Tags { get; init; }
}

