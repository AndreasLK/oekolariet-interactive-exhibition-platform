namespace ContentService.Facade.Dtos;

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

