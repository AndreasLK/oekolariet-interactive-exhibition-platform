using System.Net;
using System.Text;
using ContentService.Sdk.Clients;
using ContentService.Sdk.Exceptions;
using ContentService.Sdk.Options;
using ContentService.Tests.Sdk.TestDoubles;

namespace ContentService.Tests.Sdk;

/// <summary>
/// Unit tests verifying HTTP error mapping, RFC 7807 ProblemDetails parsing, and bounded non-JSON diagnostics.
/// </summary>
public sealed class ContentServiceErrorMappingTests
{
        private const string DEFAULT_BASE_URL = "https://content.oekolariet.dk/";
        private const int BOUNDED_ERROR_BODY_MAX_BYTES = 16 * 1024;

        /// <summary>
        /// Verifies that ProblemDetails JSON responses populate <see cref="ContentServiceApiException.ProblemDetails"/> accurately.
        /// </summary>
        [Fact]
        public async Task SendRequests_WhenProblemDetailsReturned_MapsStatusCodeAndProblemDetailsAsync()
        {
                StubHttpMessageHandler handler = new(responder: (_, _) =>
                {
                        HttpResponseMessage response = new(statusCode: HttpStatusCode.InsufficientStorage)
                        {
                                Content = new StringContent(
                                        content: """{"title":"Storage quota exceeded.","status":507,"detail":"Quota full."}""",
                                        encoding: Encoding.UTF8,
                                        mediaType: "application/problem+json")
                        };
                        return Task.FromResult(response);
                });

                ContentServiceImageClient client = CreateClient(handler: handler);

                ContentServiceApiException exception = await Assert.ThrowsAsync<ContentServiceApiException>(
                        testCode: () => client.GetImageMetadataAsync(imageId: Guid.NewGuid()));

                Assert.Equal(expected: HttpStatusCode.InsufficientStorage, actual: exception.StatusCode);
                Assert.Equal(expected: "Quota full.", actual: exception.ProblemDetails?.Detail);
                Assert.Null(exception.RawResponseBody);
        }

        /// <summary>
        /// Verifies that oversized HTML proxy errors are truncated to the 16 KiB safety bound without throwing JsonException.
        /// </summary>
        [Fact]
        public async Task DownloadImageAsync_WhenNonJsonHtmlReturned_TruncatesRawBodyAndDisposesResponseAsync()
        {
                string hugeHtml = new(c: 'x', count: BOUNDED_ERROR_BODY_MAX_BYTES + 2048);
                DisposalTrackingHttpResponseMessage? trackedResponse = null;

                StubHttpMessageHandler handler = new(responder: (_, _) =>
                {
                        trackedResponse = new DisposalTrackingHttpResponseMessage(statusCode: HttpStatusCode.BadGateway)
                        {
                                Content = new StringContent(
                                        content: hugeHtml,
                                        encoding: Encoding.UTF8,
                                        mediaType: "text/html")
                        };
                        return Task.FromResult<HttpResponseMessage>(trackedResponse);
                });

                ContentServiceImageClient client = CreateClient(handler: handler);

                ContentServiceApiException exception = await Assert.ThrowsAsync<ContentServiceApiException>(
                        testCode: () => client.DownloadImageAsync(imageId: Guid.NewGuid()));

                Assert.Equal(expected: HttpStatusCode.BadGateway, actual: exception.StatusCode);
                Assert.Null(exception.ProblemDetails);
                Assert.NotNull(exception.RawResponseBody);
                Assert.Equal(expected: BOUNDED_ERROR_BODY_MAX_BYTES, actual: exception.RawResponseBody.Length);
                Assert.Equal(expected: 1, actual: trackedResponse!.DisposeCount);
        }

        /// <summary>
        /// Verifies that empty error bodies (such as unmatched 404 or 499) map cleanly without null-body failures.
        /// </summary>
        [Fact]
        public async Task GetImageMetadataAsync_WhenEmptyErrorBodyReturned_ThrowsContentServiceApiExceptionAsync()
        {
                StubHttpMessageHandler handler = new(responder: (_, _) =>
                        Task.FromResult(new HttpResponseMessage(statusCode: HttpStatusCode.NotFound)));

                ContentServiceImageClient client = CreateClient(handler: handler);

                ContentServiceApiException exception = await Assert.ThrowsAsync<ContentServiceApiException>(
                        testCode: () => client.GetImageMetadataAsync(imageId: Guid.NewGuid()));

                Assert.Equal(expected: HttpStatusCode.NotFound, actual: exception.StatusCode);
                Assert.Null(exception.ProblemDetails);
                Assert.Null(exception.RawResponseBody);
        }

        private static ContentServiceImageClient CreateClient(HttpMessageHandler handler)
        {
                Uri baseAddress = new(uriString: DEFAULT_BASE_URL, uriKind: UriKind.Absolute);
                HttpClient httpClient = new(handler: handler) { BaseAddress = baseAddress };
                return new ContentServiceImageClient(
                        httpClient: httpClient,
                        options: Microsoft.Extensions.Options.Options.Create(options: new ContentServiceClientOptions
                        {
                                BaseAddress = baseAddress
                        }));
        }
}

