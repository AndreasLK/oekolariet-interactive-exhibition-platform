using System.Net;
using System.Net.Http.Headers;
using ContentService.Sdk.Clients;
using ContentService.Sdk.Models;
using ContentService.Sdk.Options;
using ContentService.Tests.Sdk.TestDoubles;

namespace ContentService.Tests.Sdk;

/// <summary>
/// Unit tests verifying image binary downloads, <c>FileNameStar</c> header parsing, and response disposal ownership.
/// </summary>
public sealed class ContentServiceImageDownloadTests
{
        private const string BASE_URL_WITH_SUBPATH = "https://content.oekolariet.dk/content";
        private const string DEFAULT_BASE_URL = "https://content.oekolariet.dk/";
        private const string PNG_MEDIA_TYPE = "image/png";
        private const string DANISH_FILE_NAME = "økolariet æble.png";

        /// <summary>
        /// Verifies that DownloadImageAsync extracts FileNameStar, Content-Type, Content-Length, streams bytes, and disposes once.
        /// </summary>
        [Fact]
        public async Task DownloadImageAsync_WhenValid_ExtractsFileNameStarAndDisposesResponseIdempotentlyAsync()
        {
                byte[] imageBytes = [10, 20, 30, 40, 50];
                Guid imageId = Guid.NewGuid();
                DisposalTrackingHttpResponseMessage? trackedResponse = null;

                StubHttpMessageHandler handler = new(responder: (_, _) =>
                {
                        trackedResponse = new DisposalTrackingHttpResponseMessage(statusCode: HttpStatusCode.OK)
                        {
                                Content = new ByteArrayContent(content: imageBytes)
                        };
                        trackedResponse.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType: PNG_MEDIA_TYPE);
                        trackedResponse.Content.Headers.ContentLength = imageBytes.Length;
                        trackedResponse.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue(dispositionType: "inline")
                        {
                                FileNameStar = DANISH_FILE_NAME
                        };
                        return Task.FromResult<HttpResponseMessage>(trackedResponse);
                });

                ContentServiceImageClient client = CreateClient(
                        handler: handler,
                        baseAddress: new Uri(uriString: BASE_URL_WITH_SUBPATH, uriKind: UriKind.Absolute));

                ContentImageDownload download = await client.DownloadImageAsync(imageId: imageId);
                await using (download)
                {
                        using MemoryStream copied = new();
                        await download.Content.CopyToAsync(destination: copied);

                        Assert.Equal(expected: imageBytes, actual: copied.ToArray());
                        Assert.Equal(expected: PNG_MEDIA_TYPE, actual: download.ContentType);
                        Assert.Equal(expected: DANISH_FILE_NAME, actual: download.FileName);
                        Assert.Equal(expected: imageBytes.Length, actual: download.ContentLength);
                        Assert.Equal(expected: 0, actual: trackedResponse!.DisposeCount);
                }

                // Calling Dispose after DisposeAsync must stay idempotent without double-disposing the underlying response.
                download.Dispose();
                Assert.Equal(expected: 1, actual: trackedResponse!.DisposeCount);
                Assert.Equal(
                        expected: $"https://content.oekolariet.dk/content/api/images/{imageId:D}/content",
                        actual: handler.LastRequest?.RequestUri?.AbsoluteUri);
        }

        /// <summary>
        /// Verifies that DownloadImageAsync disposes the underlying HttpResponseMessage if stream extraction throws post-headers.
        /// </summary>
        [Fact]
        public async Task DownloadImageAsync_WhenStreamExtractionThrows_DisposesHttpResponseMessageAsync()
        {
                DisposalTrackingHttpResponseMessage? trackedResponse = null;

                StubHttpMessageHandler handler = new(responder: (_, _) =>
                {
                        trackedResponse = new DisposalTrackingHttpResponseMessage(statusCode: HttpStatusCode.OK)
                        {
                                Content = new FaultingReadStreamHttpContent()
                        };
                        return Task.FromResult<HttpResponseMessage>(trackedResponse);
                });

                ContentServiceImageClient client = CreateClient(
                        handler: handler,
                        baseAddress: new Uri(uriString: DEFAULT_BASE_URL, uriKind: UriKind.Absolute));

                await Assert.ThrowsAsync<IOException>(
                        testCode: () => client.DownloadImageAsync(imageId: Guid.NewGuid()));

                Assert.NotNull(trackedResponse);
                Assert.Equal(expected: 1, actual: trackedResponse.DisposeCount);
        }

        /// <summary>
        /// Verifies that DownloadImageAsync rejects <see cref="Guid.Empty"/> before issuing an HTTP request.
        /// </summary>
        [Fact]
        public async Task DownloadImageAsync_WhenGuidEmpty_ThrowsArgumentExceptionAsync()
        {
                StubHttpMessageHandler handler = new(responder: (_, _) => Task.FromResult(new HttpResponseMessage()));
                ContentServiceImageClient client = CreateClient(
                        handler: handler,
                        baseAddress: new Uri(uriString: DEFAULT_BASE_URL, uriKind: UriKind.Absolute));

                await Assert.ThrowsAsync<ArgumentException>(
                        testCode: () => client.DownloadImageAsync(imageId: Guid.Empty));
                Assert.Null(handler.LastRequest);
        }

        private static ContentServiceImageClient CreateClient(HttpMessageHandler handler, Uri baseAddress)
        {
                HttpClient httpClient = new(handler: handler) { BaseAddress = baseAddress };
                return new ContentServiceImageClient(
                        httpClient: httpClient,
                        options: Microsoft.Extensions.Options.Options.Create(options: new ContentServiceClientOptions
                        {
                                BaseAddress = baseAddress
                        }));
        }
}

