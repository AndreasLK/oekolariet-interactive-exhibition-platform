namespace ContentService.Tests.Sdk.TestDoubles;

/// <summary>
/// Captures outgoing SDK HTTP requests in memory and replays deterministic responses without network or Docker dependencies.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

        /// <summary>
        /// Initializes a new instance of <see cref="StubHttpMessageHandler"/> with an asynchronous response delegate.
        /// </summary>
        /// <param name="responder">Delegate producing the simulated HTTP response for each captured request.</param>
        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        {
                ArgumentNullException.ThrowIfNull(responder);
                this._responder = responder;
        }

        /// <summary>
        /// Gets the most recently sent <see cref="HttpRequestMessage"/>.
        /// </summary>
        public HttpRequestMessage? LastRequest { get; private set; }

        /// <summary>
        /// Gets the raw request body bytes captured for non-multipart requests.
        /// </summary>
        public byte[]? LastRequestBodyBytes { get; private set; }

        /// <summary>
        /// Gets the multipart form-data sections parsed from the most recent multipart upload request.
        /// </summary>
        public IReadOnlyList<CapturedMultipartPart> LastMultipartParts { get; private set; } = [];

        /// <inheritdoc />
        protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
        {
                this.LastRequest = request;

                if (request.Content is MultipartFormDataContent multipart)
                {
                        // Read each multipart section once so non-seekable StreamContent parts are never double-consumed.
                        this.LastMultipartParts = await CaptureMultipartPartsAsync(
                                multipart: multipart,
                                cancellationToken: cancellationToken);
                }
                else if (request.Content is not null)
                {
                        this.LastRequestBodyBytes = await request.Content.ReadAsByteArrayAsync(
                                cancellationToken: cancellationToken);
                }

                return await this._responder(arg1: request, arg2: cancellationToken);
        }

        /// <summary>
        /// Extracts headers and payload bytes from each part inside a multipart request.
        /// </summary>
        /// <param name="multipart">The outgoing multipart form-data content.</param>
        /// <param name="cancellationToken">A token to cancel reading.</param>
        /// <returns>The captured multipart sections.</returns>
        private static async Task<IReadOnlyList<CapturedMultipartPart>> CaptureMultipartPartsAsync(
                MultipartFormDataContent multipart,
                CancellationToken cancellationToken)
        {
                List<CapturedMultipartPart> captured = [];
                foreach (HttpContent part in multipart)
                {
                        byte[] bytes = await part.ReadAsByteArrayAsync(cancellationToken: cancellationToken);
                        captured.Add(new CapturedMultipartPart(
                                Name: part.Headers.ContentDisposition?.Name?.Trim('"'),
                                FileName: part.Headers.ContentDisposition?.FileName?.Trim('"'),
                                FileNameStar: part.Headers.ContentDisposition?.FileNameStar,
                                ContentType: part.Headers.ContentType?.MediaType,
                                Bytes: bytes));
                }

                return captured;
        }
}

