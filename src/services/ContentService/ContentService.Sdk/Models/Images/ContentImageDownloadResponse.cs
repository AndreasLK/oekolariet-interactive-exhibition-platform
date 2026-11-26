using System.Net.Http.Headers;

namespace ContentService.Sdk.Models;

/// <summary>
/// Represents a streamed image download response from ContentService, owning disposal of both the response stream and underlying HTTP response.
/// </summary>
/// <remarks>
/// Non-standard streaming ownership: Unlike <c>GetByteArrayAsync</c> or buffered downloads, this type holds the live
/// <see cref="HttpResponseMessage"/> open so callers stream image bytes directly off the socket without LOH buffering.
/// <para>
/// What we gain: Low memory footprint for large exhibition assets.
/// What we lose: Callers must deterministically dispose this wrapper (<c>await using</c> or <c>using</c>) to release the connection.
/// </para>
/// </remarks>
public sealed class ContentImageDownloadResponse : IDisposable, IAsyncDisposable
{
        private readonly HttpResponseMessage? _response;
        private int _disposed;

        /// <summary>
        /// Initializes a new instance of <see cref="ContentImageDownloadResponse"/> from an in-memory or caller-supplied stream (for tests or adapters).
        /// </summary>
        /// <param name="content">The readable image stream.</param>
        /// <param name="contentType">The MIME content type reported for the image, if known.</param>
        /// <param name="fileName">The suggested file name, if known.</param>
        /// <param name="contentLength">The payload length in bytes, if reported.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="content"/> is <see langword="null"/>.</exception>
        public ContentImageDownloadResponse(
                Stream content,
                string? contentType,
                string? fileName,
                long? contentLength)
                : this(
                        content: content,
                        contentType: contentType,
                        fileName: fileName,
                        contentLength: contentLength,
                        response: null)
        {
        }

        internal ContentImageDownloadResponse(
                Stream content,
                string? contentType,
                string? fileName,
                long? contentLength,
                HttpResponseMessage? response)
        {
                ArgumentNullException.ThrowIfNull(content);

                this.Content = content;
                this.ContentType = contentType;
                this.FileName = fileName;
                this.ContentLength = contentLength;
                this._response = response;
        }

        /// <summary>
        /// Gets the readable binary stream containing the image payload.
        /// </summary>
        public Stream Content { get; }

        /// <summary>
        /// Gets the MIME content type from the <c>Content-Type</c> header, or <see langword="null"/> if absent.
        /// </summary>
        public string? ContentType { get; }

        /// <summary>
        /// Gets the file name extracted from <c>Content-Disposition</c>, or <see langword="null"/> if absent.
        /// </summary>
        public string? FileName { get; }

        /// <summary>
        /// Gets the payload length in bytes from <c>Content-Length</c>, or <see langword="null"/> when unknown.
        /// </summary>
        public long? ContentLength { get; }

        /// <summary>
        /// Disposes the underlying binary <see cref="Content"/> stream and the owning <see cref="HttpResponseMessage"/>.
        /// </summary>
        public void Dispose()
        {
                if (Interlocked.Exchange(location1: ref this._disposed, value: 1) != 0)
                {
                        return;
                }

                this.Content.Dispose();
                this._response?.Dispose();
        }

        /// <summary>
        /// Asynchronously disposes the underlying binary <see cref="Content"/> stream and the owning <see cref="HttpResponseMessage"/>.
        /// </summary>
        /// <returns>A value task representing the asynchronous dispose operation.</returns>
        public async ValueTask DisposeAsync()
        {
                if (Interlocked.Exchange(location1: ref this._disposed, value: 1) != 0)
                {
                        return;
                }

                await this.Content.DisposeAsync().ConfigureAwait(false);
                this._response?.Dispose();
        }

        /// <summary>
        /// Creates a <see cref="ContentImageDownloadResponse"/> from a streamed HTTP response, extracting content metadata headers.
        /// </summary>
        /// <param name="response">The successful HTTP response whose lifetime is transferred to the created instance.</param>
        /// <param name="contentStream">The open response payload stream.</param>
        /// <returns>A new <see cref="ContentImageDownloadResponse"/> owning both <paramref name="contentStream"/> and <paramref name="response"/>.</returns>
        internal static ContentImageDownloadResponse FromHttpResponse(
                HttpResponseMessage response,
                Stream contentStream)
        {
                string? contentType = response.Content.Headers.ContentType?.MediaType;
                string? fileName = ExtractResponseFileName(disposition: response.Content.Headers.ContentDisposition);
                long? contentLength = response.Content.Headers.ContentLength;

                return new ContentImageDownloadResponse(
                        content: contentStream,
                        contentType: contentType,
                        fileName: fileName,
                        contentLength: contentLength,
                        response: response);
        }

        private static string? ExtractResponseFileName(ContentDispositionHeaderValue? disposition)
        {
                if (disposition is null)
                {
                        return null;
                }

                // ImageEndpoints sets UTF-8 FileNameStar only; fall back to unquoted FileName if an intermediary provides it.
                if (!string.IsNullOrWhiteSpace(disposition.FileNameStar))
                {
                        return disposition.FileNameStar;
                }

                return string.IsNullOrWhiteSpace(disposition.FileName)
                        ? null
                        : disposition.FileName.Trim('"');
        }
}

