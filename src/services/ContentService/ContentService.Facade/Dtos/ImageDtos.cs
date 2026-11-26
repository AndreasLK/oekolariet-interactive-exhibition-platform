using ContentService.Facade.Common;

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

/// <summary>
/// Command DTO for updating an active image's searchable metadata with optimistic concurrency checking.
/// </summary>
public sealed record class UpdateImageMetadataCommandDto
{
        /// <summary>
        /// Gets the unique identifier of the image to update.
        /// </summary>
        public required Guid ImageId { get; init; }

        /// <summary>
        /// Gets the updated accessibility alternative text, or <see langword="null"/> to clear it.
        /// </summary>
        public string? AltText { get; init; }

        /// <summary>
        /// Gets the updated comma-separated search tags, or <see langword="null"/> to clear them.
        /// </summary>
        public string? Tags { get; init; }

        /// <summary>
        /// Gets the updated recursive album path, or <see langword="null"/> for root.
        /// </summary>
        public string? AlbumPath { get; init; }

        /// <summary>
        /// Gets the expected concurrency stamp observed when the image was loaded.
        /// </summary>
        public required Guid ExpectedConcurrencyStamp { get; init; }

        /// <summary>
        /// Gets the trusted actor identifier for audit attribution.
        /// </summary>
        public required string ActorId { get; init; }
}

/// <summary>
/// Command DTO for soft-deleting an unreferenced exhibition image.
/// </summary>
public sealed record class DeleteImageCommandDto
{
        /// <summary>
        /// Gets the unique identifier of the image to soft-delete.
        /// </summary>
        public required Guid ImageId { get; init; }

        /// <summary>
        /// Gets the trusted actor identifier for audit attribution.
        /// </summary>
        public required string ActorId { get; init; }
}

/// <summary>
/// Query DTO for retrieving a paginated, filtered catalog of active exhibition images.
/// </summary>
public sealed record class ListImagesQueryDto
{
        /// <summary>
        /// Gets the requested 1-based page number.
        /// </summary>
        public int Page { get; init; } = FacadeConstants.DEFAULT_PAGE;

        /// <summary>
        /// Gets the requested number of items per page.
        /// </summary>
        public int PageSize { get; init; } = FacadeConstants.DEFAULT_PAGE_SIZE;

        /// <summary>
        /// Gets optional search text matched against file name, alt text, and tags.
        /// </summary>
        public string? SearchTerm { get; init; }

        /// <summary>
        /// Gets an optional recursive album path filter.
        /// </summary>
        public string? AlbumPath { get; init; }

        /// <summary>
        /// Gets a value indicating whether nested sub-albums should be included when <see cref="AlbumPath"/> is set.
        /// </summary>
        public bool IncludeSubalbums { get; init; } = true;
}

/// <summary>
/// Facade DTO representing persisted metadata for an active exhibition image.
/// </summary>
public sealed record class ImageMetadataDto
{
        /// <summary>
        /// Gets the unique identifier of the image.
        /// </summary>
        public required Guid Id { get; init; }

        /// <summary>
        /// Gets the shard-aware picture reference (<c>picture.{node}.{guid}</c>) used inside template schemas.
        /// </summary>
        public required string PictureReference { get; init; }

        /// <summary>
        /// Gets the URL-safe external identifier (<c>{node}_{guid}</c>) for HTTP routes.
        /// </summary>
        public required string ExternalId { get; init; }

        /// <summary>
        /// Gets the sanitized original file name.
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
        /// Gets the optional accessibility alternative text.
        /// </summary>
        public string? AltText { get; init; }

        /// <summary>
        /// Gets the optional normalized recursive album path.
        /// </summary>
        public string? AlbumPath { get; init; }

        /// <summary>
        /// Gets the normalized search tags associated with this image.
        /// </summary>
        public required IReadOnlyList<string> Tags { get; init; }

        /// <summary>
        /// Gets the UTC timestamp when the image was uploaded.
        /// </summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        /// Gets the current domain-managed optimistic concurrency stamp.
        /// </summary>
        public required Guid ConcurrencyStamp { get; init; }
}

/// <summary>
/// Disposable facade DTO pairing image metadata with an open binary stream whose ownership passes to the caller.
/// </summary>
public sealed class ImageStreamDto : IAsyncDisposable, IDisposable
{
        /// <summary>
        /// Underlying application stream result disposed alongside this DTO.
        /// </summary>
        private readonly IAsyncDisposable _innerDisposable;

        /// <summary>
        /// Initializes a new instance of <see cref="ImageStreamDto"/>.
        /// </summary>
        /// <param name="metadata">The image metadata describing the stream.</param>
        /// <param name="content">The readable binary stream.</param>
        /// <param name="innerDisposable">The underlying disposable resource owning <paramref name="content"/>.</param>
        public ImageStreamDto(
                ImageMetadataDto metadata,
                Stream content,
                IAsyncDisposable innerDisposable)
        {
                ArgumentNullException.ThrowIfNull(metadata);
                ArgumentNullException.ThrowIfNull(content);
                ArgumentNullException.ThrowIfNull(innerDisposable);

                this.Metadata = metadata;
                this.Content = content;
                this._innerDisposable = innerDisposable;
        }

        /// <summary>
        /// Gets the image metadata describing the streamed payload.
        /// </summary>
        public ImageMetadataDto Metadata { get; }

        /// <summary>
        /// Gets the readable binary stream containing the image or preview bytes.
        /// </summary>
        public Stream Content { get; }

        /// <inheritdoc />
        public void Dispose()
        {
                if (this._innerDisposable is IDisposable syncDisposable)
                {
                        syncDisposable.Dispose();
                }
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync() => this._innerDisposable.DisposeAsync();
}

