namespace HighscoreService.Tests.TestDoubles
{
        /// <summary>
        /// HTTP message handler that forwards requests to an asynchronous delegate.
        /// </summary>
        /// <param name="responder">Delegate producing the simulated HTTP response.</param>
        internal sealed class DelegateHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
        {
                private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responder = responder;

                /// <inheritdoc />
                protected override Task<HttpResponseMessage> SendAsync(
                        HttpRequestMessage request,
                        CancellationToken cancellationToken)
                {
                        // Forward request execution to the configured test responder.
                        return this._responder(request);
                }
        }
}

