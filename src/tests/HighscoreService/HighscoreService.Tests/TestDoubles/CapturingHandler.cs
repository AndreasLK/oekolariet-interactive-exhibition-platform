using System.Net;
using System.Net.Http.Json;
using HighscoreService.Sdk.Models.Highscores.Responses;

namespace HighscoreService.Tests.TestDoubles
{
        /// <summary>
        /// HTTP message handler that captures each outgoing request URI and returns an empty paged leaderboard.
        /// </summary>
        /// <param name="onRequest">Callback invoked with each intercepted request URI.</param>
        internal sealed class CapturingHandler(Action<Uri?> onRequest) : HttpMessageHandler
        {
                private readonly Action<Uri?> _onRequest = onRequest;

                /// <inheritdoc />
                protected override Task<HttpResponseMessage> SendAsync(
                        HttpRequestMessage request,
                        CancellationToken cancellationToken)
                {
                        // Record the resolved URI so tests can assert subpath preservation.
                        this._onRequest(request.RequestUri);
                        PagedHighscoreResponse empty = new([], 0, 1, 25);
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                                Content = JsonContent.Create(empty)
                        });
                }
        }
}

