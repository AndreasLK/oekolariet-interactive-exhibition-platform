using System.Net;
using System.Text;
using System.Text.Json;
using ContentService.Sdk.Clients;
using ContentService.Sdk.Models;
using ContentService.Sdk.Options;
using ContentService.Tests.Sdk.TestDoubles;

namespace ContentService.Tests.Sdk;

/// <summary>
/// Unit tests verifying image upload multipart encoding, non-seekable streams, actor header rules, and input validation.
/// </summary>
public sealed class ContentServiceImageUploadTests
{
        private const string BASE_URL_WITH_SUBPATH = "https://content.oekolariet.dk/content";
        private const string DEFAULT_BASE_URL = "https://content.oekolariet.dk/";
        private const string PNG_MEDIA_TYPE = "image/png";
        private const string DANISH_FILE_NAME = "økolariet æble.png";
        private const string ALT_TEXT_VALUE = "Udstilling billede";
        private const string DEFAULT_ACTOR_ID = "editor-default";
        private const string EXPLICIT_ACTOR_ID = "editor-user-42";

        /// <summary>
        /// Verifies that uploading a non-seekable stream with a Danish filename and alt text sends expected multipart fields and headers.
        /// </summary>
        [Fact]
        public async Task UploadImageAsync_WhenNonSeekableDanishFileWithAltText_SendsMultipartFieldsAndDeserializesMetadataAsync()
        {
                // Official 8-byte PNG magic header (0x89 + ASCII "PNG" + CRLF + Ctrl+Z + LF) simulating a valid PNG upload stream.
                byte[] payloadBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
                Guid expectedId = Guid.NewGuid();
                ContentImageMetadata expectedMetadata = CreateSampleMetadata(id: expectedId, fileName: DANISH_FILE_NAME);
                StubHttpMessageHandler handler = CreateMetadataResponseHandler(metadata: expectedMetadata);

                ContentServiceImageClient client = CreateClient(
                        handler: handler,
                        baseAddress: new Uri(uriString: BASE_URL_WITH_SUBPATH, uriKind: UriKind.Absolute),
                        defaultActorId: DEFAULT_ACTOR_ID);

                await using NonSeekableReadStream stream = new(data: payloadBytes);
                UploadImageRequest request = new()
                {
                        Content = stream,
                        FileName = DANISH_FILE_NAME,
                        ContentType = $"{PNG_MEDIA_TYPE}; charset=utf-8",
                        AltText = ALT_TEXT_VALUE,
                        ActorId = EXPLICIT_ACTOR_ID
                };

                ContentImageMetadata actual = await client.UploadImageAsync(request: request, cancellationToken: TestContext.Current.CancellationToken);

                Assert.Equal(expected: expectedMetadata, actual: actual);
                Assert.NotNull(handler.LastRequest);
                Assert.Equal(
                        expected: "https://content.oekolariet.dk/content/api/images",
                        actual: handler.LastRequest.RequestUri?.AbsoluteUri);
                Assert.True(handler.LastRequest.Headers.TryGetValues(name: "X-Actor-Id", values: out IEnumerable<string>? actorHeaders));
                Assert.Equal(expected: EXPLICIT_ACTOR_ID, actual: Assert.Single(actorHeaders));

                // Ensure FileNameStar carries UTF-8 Danish characters and MIME parameters were stripped from the part header.
                Assert.Equal(expected: 2, actual: handler.LastMultipartParts.Count);
                CapturedMultipartPart filePart = handler.LastMultipartParts[0];
                Assert.Equal(expected: "file", actual: filePart.Name);
                Assert.Equal(expected: DANISH_FILE_NAME, actual: filePart.FileNameStar);
                Assert.Equal(expected: PNG_MEDIA_TYPE, actual: filePart.ContentType);
                Assert.Equal(expected: payloadBytes, actual: filePart.Bytes);

                CapturedMultipartPart altTextPart = handler.LastMultipartParts[1];
                Assert.Equal(expected: "altText", actual: altTextPart.Name);
                Assert.Equal(expected: ALT_TEXT_VALUE, actual: Encoding.UTF8.GetString(bytes: altTextPart.Bytes));
        }

        /// <summary>
        /// Verifies that null altText and whitespace actorId are omitted so ContentService applies server fallback attribution.
        /// </summary>
        [Fact]
        public async Task UploadImageAsync_WhenAltTextNullAndActorWhitespace_OmitsOptionalPartsAndActorHeaderAsync()
        {
                ContentImageMetadata expectedMetadata = CreateSampleMetadata(id: Guid.NewGuid(), fileName: "leaf.png");
                StubHttpMessageHandler handler = CreateMetadataResponseHandler(metadata: expectedMetadata);

                ContentServiceImageClient client = CreateClient(
                        handler: handler,
                        baseAddress: new Uri(uriString: DEFAULT_BASE_URL, uriKind: UriKind.Absolute),
                        defaultActorId: "   ");

                await using MemoryStream stream = new(buffer: [1, 2, 3, 4]);
                UploadImageRequest request = new()
                {
                        Content = stream,
                        FileName = "leaf.png",
                        ContentType = PNG_MEDIA_TYPE,
                        AltText = null,
                        ActorId = "  "
                };

                _ = await client.UploadImageAsync(request: request, cancellationToken: TestContext.Current.CancellationToken);

                Assert.NotNull(handler.LastRequest);
                Assert.False(handler.LastRequest.Headers.Contains(name: "X-Actor-Id"));
                Assert.Single(handler.LastMultipartParts);
        }

        /// <summary>
        /// Verifies that invalid upload inputs fail fast client-side before issuing any HTTP request.
        /// </summary>
        [Fact]
        public async Task UploadImageAsync_WhenInputsInvalid_ThrowsArgumentExceptionWithoutSendingHttpAsync()
        {
                int callCount = 0;
                StubHttpMessageHandler handler = new(responder: (_, _) =>
                {
                        callCount++;
                        return Task.FromResult(new HttpResponseMessage(statusCode: HttpStatusCode.OK));
                });

                ContentServiceImageClient client = CreateClient(
                        handler: handler,
                        baseAddress: new Uri(uriString: DEFAULT_BASE_URL, uriKind: UriKind.Absolute));

                await using MemoryStream unreadableStream = new(buffer: [1, 2]);
                unreadableStream.Dispose();

                await Assert.ThrowsAsync<ArgumentException>(
                        testCode: () => client.UploadImageAsync(request: new UploadImageRequest
                        {
                                Content = unreadableStream,
                                FileName = "bad.png",
                                ContentType = PNG_MEDIA_TYPE
                        }, cancellationToken: TestContext.Current.CancellationToken));

                await using MemoryStream readableStream = new(buffer: [1, 2]);
                await Assert.ThrowsAsync<ArgumentException>(
                        testCode: () => client.UploadImageAsync(request: new UploadImageRequest
                        {
                                Content = readableStream,
                                FileName = "bad\"name.png",
                                ContentType = PNG_MEDIA_TYPE
                        }, cancellationToken: TestContext.Current.CancellationToken));

                await Assert.ThrowsAsync<ArgumentException>(
                        testCode: () => client.UploadImageAsync(request: new UploadImageRequest
                        {
                                Content = readableStream,
                                FileName = "valid.png",
                                ContentType = "not-a-mime-type"
                        }, cancellationToken: TestContext.Current.CancellationToken));

                await Assert.ThrowsAsync<ArgumentException>(
                        testCode: () => client.UploadImageAsync(request: new UploadImageRequest
                        {
                                Content = readableStream,
                                FileName = "valid.png",
                                ContentType = PNG_MEDIA_TYPE,
                                ActorId = "Søren"
                        }, cancellationToken: TestContext.Current.CancellationToken));

                Assert.Equal(expected: 0, actual: callCount);
        }

        private static StubHttpMessageHandler CreateMetadataResponseHandler(ContentImageMetadata metadata) =>
                new(responder: (_, _) => Task.FromResult(new HttpResponseMessage(statusCode: HttpStatusCode.Created)
                {
                        Content = new StringContent(
                                content: JsonSerializer.Serialize(value: metadata),
                                encoding: Encoding.UTF8,
                                mediaType: "application/json")
                }));

        private static ContentServiceImageClient CreateClient(
                HttpMessageHandler handler,
                Uri baseAddress,
                string? defaultActorId = null)
        {
                HttpClient httpClient = new(handler: handler) { BaseAddress = baseAddress };
                return new ContentServiceImageClient(
                        httpClient: httpClient,
                        options: Microsoft.Extensions.Options.Options.Create(options: new ContentServiceClientOptions
                        {
                                BaseAddress = baseAddress,
                                DefaultActorId = defaultActorId
                        }));
        }

        private static ContentImageMetadata CreateSampleMetadata(Guid id, string fileName) =>
                new()
                {
                        Id = id,
                        PictureReference = $"picture.node-1.{id:D}",
                        ExternalId = $"node-1_{id:D}",
                        FileName = fileName,
                        ContentType = PNG_MEDIA_TYPE,
                        SizeBytes = 8,
                        Sha256Hash = new string(c: 'a', count: 64),
                        AltText = ALT_TEXT_VALUE,
                        CreatedAt = DateTimeOffset.UtcNow,
                        ConcurrencyStamp = Guid.NewGuid()
                };
}

