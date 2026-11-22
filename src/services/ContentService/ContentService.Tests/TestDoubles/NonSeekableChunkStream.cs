namespace ContentService.Tests.TestDoubles;

/// <summary>
/// Non-seekable stream emitting deterministic bytes in small chunks to verify streaming overflow protection.
/// </summary>
internal sealed class NonSeekableChunkStream : Stream
{
        private const byte FILL_BYTE = 0x89;

        private readonly int _totalBytes;
        private int _bytesEmitted;

        public NonSeekableChunkStream(int totalBytes) => this._totalBytes = totalBytes;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
                int remaining = this._totalBytes - this._bytesEmitted;
                if (remaining <= 0)
                {
                        return 0;
                }

                int toCopy = Math.Min(val1: count, val2: remaining);
                Array.Fill(array: buffer, value: FILL_BYTE, startIndex: offset, count: toCopy);
                this._bytesEmitted += toCopy;
                return toCopy;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

