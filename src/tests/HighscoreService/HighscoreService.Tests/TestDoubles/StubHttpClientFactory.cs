namespace HighscoreService.Tests.TestDoubles
{
        /// <summary>
        /// Minimal <see cref="IHttpClientFactory"/> test double backed by a delegate responder.
        /// </summary>
        /// <param name="responder">Delegate invoked for each outgoing HTTP request.</param>
        internal sealed class StubHttpClientFactory(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : IHttpClientFactory
        {
                private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responder = responder;

                /// <inheritdoc />
                public HttpClient CreateClient(string name)
                {
                        // Wrap the delegate handler in a new HttpClient instance per call.
                        return new HttpClient(new DelegateHandler(this._responder));
                }
        }
}

