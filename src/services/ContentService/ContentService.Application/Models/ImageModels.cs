using ContentService.Application.Common;

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
}

/// <summary>
/// Immutable metadata representation of a stored exhibition image.
/// </summary>
public sealed record class ImageMetadataResult
{
        /// <summary>
        /// Gets the unique identifier of the image.
        /// </summary>
        public required Guid Id { get; init; }

        /// <summary>
        /// Gets the picture reference (<c>picture.{node}.{guid}</c>) used inside template JSON schemas.
        /// </summary>
        public required string PictureReference { get; init; }

        /// <summary>
        /// Gets the URL-safe external asset identifier (<c>{node}_{guid}</c>) for HTTP API routes.
        /// </summary>
        public required string ExternalId { get; init; }

        /// <summary>
        /// Gets the sanitized original file name of the stored image.
        /// </summary>
        public required string FileName { get; init; }

        /// <summary>
        /// Gets the normalized lowercase MIME content type.
        /// </summary>
        public required string ContentType { get; init; }

        /// <summary>
        /// Gets the verified payload size in bytes.
        /// </summary>
        public required long SizeBytes { get; init; }

        /// <summary>
        /// Gets the lowercase 64-character hexadecimal SHA-256 digest of the image payload.
        /// </summary>
        public required string Sha256Hash { get; init; }

        /// <summary>
        /// Gets the optional accessibility alternative text, or <see langword="null"/> when none was provided.
        /// </summary>
        public string? AltText { get; init; }

        /// <summary>
        /// Gets the UTC timestamp when the image was uploaded.
        /// </summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        /// Gets the domain-managed optimistic edit-lock stamp for this image record.
        /// </summary>
        public required Guid ConcurrencyStamp { get; init; }
}

/// <summary>
/// Query criteria for retrieving a paginated list of active exhibition images.
/// </summary>
public sealed record class ListImagesQuery
{
        /// <summary>
        /// Gets the requested 1-based page number.
        /// </summary>
        public int Page { get; init; } = ApplicationConstants.DEFAULT_PAGE;

        /// <summary>
        /// Gets the requested number of items per page.
        /// </summary>
        public int PageSize { get; init; } = ApplicationConstants.DEFAULT_PAGE_SIZE;

        /// <summary>
        /// Gets optional search text matched against image file names.
        /// </summary>
        public string? SearchTerm { get; init; }
}

/// <summary>
/// Query criteria for loading an exhibition image's readable byte stream.
/// </summary>
public sealed record class GetImageStreamQuery
{
        /// <summary>
        /// Gets the unique identifier of the requested image.
        /// </summary>
        public required Guid ImageId { get; init; }
}

/// <summary>
/// Represents an image metadata + readable payload stream result, owning disposal of <see cref="Content"/>.
/// </summary>
public sealed record class ImageStreamResult : IDisposable, IAsyncDisposable
{
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageStreamResult"/> class.
        /// </summary>
        /// <param name="metadata">The image metadata.</param>
        /// <param name="content">The open, caller-owned image payload stream.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="metadata"/> or <paramref name="content"/> is <see langword="null"/>.</exception>
        public ImageStreamResult(ImageMetadataResult metadata, Stream content)
        {
                ArgumentNullException.ThrowIfNull(metadata);
                ArgumentNullException.ThrowIfNull(content);

                this.Metadata = metadata;
                this.Content = content;
        }

        /// <summary>
        /// Gets the metadata of the requested image.
        /// </summary>
        public ImageMetadataResult Metadata { get; }

        /// <summary>
        /// Gets the readable binary stream containing the image payload.
        /// </summary>
        public Stream Content { get; }

        /// <summary>
        /// Disposes the underlying binary <see cref="Content"/> stream.
        /// </summary>
        public void Dispose()
        {
                this.Content.Dispose();
        }

        /// <summary>
        /// Asynchronously disposes the underlying binary <see cref="Content"/> stream.
        /// </summary>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous dispose operation.</returns>
        public ValueTask DisposeAsync()
        {
                return this.Content.DisposeAsync();
        }
}

/// <summary>
/// Command payload for soft-deleting an unreferenced exhibition image.
/// </summary>
public sealed record class DeleteImageCommand
{
        /// <summary>
        /// Gets the unique identifier of the image to soft-delete.
        /// </summary>
        public required Guid ImageId { get; init; }

        /// <summary>
        /// Gets the identifier of the user or service performing the deletion for audit logging.
        /// </summary>
        public required string ActorId { get; init; }
}
