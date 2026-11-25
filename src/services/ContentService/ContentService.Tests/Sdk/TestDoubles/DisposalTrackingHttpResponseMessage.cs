using System.Net;

namespace ContentService.Tests.Sdk.TestDoubles;

/// <summary>
/// Test <see cref="HttpResponseMessage"/> recording how many times <see cref="Dispose(bool)"/> executes.
/// </summary>
internal sealed class DisposalTrackingHttpResponseMessage : HttpResponseMessage
{
        /// <summary>
        /// Initializes a new instance of <see cref="DisposalTrackingHttpResponseMessage"/> with the given status code.
        /// </summary>
        /// <param name="statusCode">The HTTP status code.</param>
        public DisposalTrackingHttpResponseMessage(HttpStatusCode statusCode)
                : base(statusCode: statusCode)
        {
        }

        /// <summary>
        /// Gets the total number of times this response instance has been disposed.
        /// </summary>
        public int DisposeCount { get; private set; }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
                if (disposing)
                {
                        // Track exact disposal count so idempotency and leak-prevention tests can assert once-only cleanup.
                        this.DisposeCount++;
                }

                base.Dispose(disposing: disposing);
        }
}

