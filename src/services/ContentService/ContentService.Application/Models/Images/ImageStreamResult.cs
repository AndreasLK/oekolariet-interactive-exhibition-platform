namespace ContentService.Application.Models;

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

