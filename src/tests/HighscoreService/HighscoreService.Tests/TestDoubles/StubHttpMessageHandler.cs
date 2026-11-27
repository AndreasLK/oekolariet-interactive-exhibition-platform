namespace HighscoreService.Tests.TestDoubles
{
        /// <summary>
        /// Cancellation-aware HTTP message handler test double for SDK unit tests.
        /// </summary>
        /// <param name="handler">Delegate handling each request and cancellation token.</param>
        internal sealed class StubHttpMessageHandler(
                Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
        {
                private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler = handler;

                /// <inheritdoc />
                protected override Task<HttpResponseMessage> SendAsync(
                        HttpRequestMessage request,
                        CancellationToken cancellationToken)
                {
                        // Delegate directly so tests can simulate timeouts, cancellations, and HTTP status codes.
                        return this._handler(request, cancellationToken);
                }
        }
}

