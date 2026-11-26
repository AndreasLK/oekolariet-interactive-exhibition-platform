namespace ContentService.Tests.TestDoubles;

/// <summary>
/// MemoryStream test double reporting <see cref="CanRead"/> as <see langword="false"/>.
/// </summary>
internal sealed class UnreadableMemoryStream : MemoryStream
{
        public UnreadableMemoryStream(byte[] buffer)
                : base(buffer: buffer, writable: false)
        {
        }

        public override bool CanRead => false;
}

