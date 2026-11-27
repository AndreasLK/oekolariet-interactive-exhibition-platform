namespace ContentService.Infrastructure.Nodes;

/// <summary>
/// Wraps an HTTP response stream together with its owning <see cref="HttpResponseMessage"/> so ASP.NET Core <c>Results.Stream</c> disposes both when streaming completes.
/// </summary>
internal sealed class HttpResponseOwningStream : Stream
{
        /// <summary>
        /// The underlying HTTP content stream.
        /// </summary>
        private readonly Stream _innerStream;

        /// <summary>
        /// The owning HTTP response message kept alive until stream disposal.
        /// </summary>
        private readonly HttpResponseMessage _response;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpResponseOwningStream"/> class.
        /// </summary>
        /// <param name="innerStream">The readable response content stream.</param>
        /// <param name="response">The owning <see cref="HttpResponseMessage"/>.</param>
        public HttpResponseOwningStream(
                Stream innerStream,
                HttpResponseMessage response)
        {
                ArgumentNullException.ThrowIfNull(innerStream);
                ArgumentNullException.ThrowIfNull(response);

                this._innerStream = innerStream;
                this._response = response;
        }

        /// <inheritdoc />
        public override bool CanRead => this._innerStream.CanRead;

        /// <inheritdoc />
        public override bool CanSeek => this._innerStream.CanSeek;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override long Length => this._innerStream.Length;

        /// <inheritdoc />
        public override long Position
        {
                get => this._innerStream.Position;
                set => this._innerStream.Position = value;
        }

        /// <inheritdoc />
        public override void Flush() => this._innerStream.Flush();

        /// <inheritdoc />
        public override Task FlushAsync(CancellationToken cancellationToken) =>
                this._innerStream.FlushAsync(cancellationToken: cancellationToken);

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) =>
                this._innerStream.Read(buffer: buffer, offset: offset, count: count);

        /// <inheritdoc />
        public override int Read(Span<byte> buffer) =>
                this._innerStream.Read(buffer: buffer);

        /// <inheritdoc />
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
                this._innerStream.ReadAsync(buffer: buffer, offset: offset, count: count, cancellationToken: cancellationToken);

        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
                this._innerStream.ReadAsync(buffer: buffer, cancellationToken: cancellationToken);

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) =>
                this._innerStream.Seek(offset: offset, origin: origin);

        /// <inheritdoc />
        public override void SetLength(long value) =>
                throw new NotSupportedException();

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) =>
                throw new NotSupportedException();

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
                if (disposing)
                {
                        // WHY: Dispose the inner stream before the HttpResponseMessage so the network socket/connection returns cleanly to the pool.
                        this._innerStream.Dispose();
                        this._response.Dispose();
                }

                base.Dispose(disposing: disposing);
        }

        /// <inheritdoc />
        public override async ValueTask DisposeAsync()
        {
                await this._innerStream.DisposeAsync();
                this._response.Dispose();
                await base.DisposeAsync();
        }
}

