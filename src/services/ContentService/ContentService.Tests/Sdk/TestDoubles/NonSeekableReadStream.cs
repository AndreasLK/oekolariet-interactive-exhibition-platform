namespace ContentService.Tests.Sdk.TestDoubles;

/// <summary>
/// Read-only non-seekable stream simulating browser <c>IBrowserFile.OpenReadStream</c> payloads.
/// </summary>
internal sealed class NonSeekableReadStream : Stream
{
        private readonly MemoryStream _inner;

        /// <summary>
        /// Initializes a new instance of <see cref="NonSeekableReadStream"/> backed by the given byte array.
        /// </summary>
        /// <param name="data">The underlying readable payload bytes.</param>
        public NonSeekableReadStream(byte[] data)
        {
                this._inner = new MemoryStream(buffer: data);
        }

        /// <inheritdoc />
        public override bool CanRead => true;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override long Length => throw new NotSupportedException("Non-seekable stream does not expose Length.");

        /// <inheritdoc />
        public override long Position
        {
                get => throw new NotSupportedException("Non-seekable stream does not expose Position.");
                set => throw new NotSupportedException("Non-seekable stream does not support setting Position.");
        }

        /// <inheritdoc />
        public override void Flush() => throw new NotSupportedException();

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) =>
                this._inner.Read(buffer: buffer, offset: offset, count: count);

        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
                this._inner.ReadAsync(buffer: buffer, cancellationToken: cancellationToken);

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
                if (disposing)
                {
                        this._inner.Dispose();
                }

                base.Dispose(disposing: disposing);
        }
}

