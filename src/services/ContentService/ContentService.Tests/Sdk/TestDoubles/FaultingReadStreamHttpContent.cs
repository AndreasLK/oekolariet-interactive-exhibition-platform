using System.Net;

namespace ContentService.Tests.Sdk.TestDoubles;

/// <summary>
/// Fault-injecting <see cref="HttpContent"/> whose <see cref="CreateContentReadStreamAsync"/> throws <see cref="IOException"/>.
/// </summary>
internal sealed class FaultingReadStreamHttpContent : HttpContent
{
        /// <inheritdoc />
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
                Task.CompletedTask;

        /// <inheritdoc />
        protected override Task<Stream> CreateContentReadStreamAsync() =>
                Task.FromException<Stream>(exception: new IOException("Simulated stream open failure."));

        /// <inheritdoc />
        protected override bool TryComputeLength(out long length)
        {
                length = 0;
                return false;
        }
}

