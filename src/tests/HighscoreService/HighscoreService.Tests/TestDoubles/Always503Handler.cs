using System.Net;

namespace HighscoreService.Tests.TestDoubles
{
        /// <summary>
        /// HTTP message handler test double that always returns HTTP 503 Service Unavailable.
        /// </summary>
        internal sealed class Always503Handler : HttpMessageHandler
        {
                /// <inheritdoc />
                protected override Task<HttpResponseMessage> SendAsync(
                        HttpRequestMessage request,
                        CancellationToken cancellationToken)
                {
                        // Simulate an unavailable primary Hub to verify SDK fallback behavior.
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
                }
        }
}

