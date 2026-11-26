using System.Net;
using System.Text;
using System.Text.Json;
using ContentService.Sdk.Clients;
using ContentService.Sdk.Models;
using ContentService.Sdk.Options;
using ContentService.Tests.Sdk.TestDoubles;

namespace ContentService.Tests.Sdk;

/// <summary>
/// Unit tests verifying album queries, scoped image queries, and metadata patch requests in <see cref="ContentServiceImageClient"/>.
/// </summary>
public sealed class ContentServiceAlbumAndMetadataTests
{
        private const string BASE_URL = "https://content.oekolariet.dk/content";
        private const string DEFAULT_ACTOR_ID = "editor-service";

        /// <summary>
        /// Verifies <see cref="ContentServiceImageClient.ListImagesAsync(ContentImageQuery, CancellationToken)"/> formats album and sub-album query parameters.
        /// </summary>
        [Fact]
        public async Task ListImagesAsync_WithAlbumQuery_FormatsAlbumAndSearchQueryAsync()
        {
                // Arrange
                ContentPagedResponse<ContentImageResponse> expected = new()
                {
                        Items = [],
                        Page = 1,
                        PageSize = 24,
                        TotalCount = 0
                };
                StubHttpMessageHandler handler = new(responder: (_, _) => Task.FromResult(result: new HttpResponseMessage(statusCode: HttpStatusCode.OK)
                {
                        Content = new StringContent(
                                content: JsonSerializer.Serialize(value: expected),
                                encoding: Encoding.UTF8,
                                mediaType: "application/json")
                }));
                ContentServiceImageClient client = CreateClient(handler: handler);

                // Act
                _ = await client.ListImagesAsync(query: new ContentImageQuery
                {
                        Page = 1,
                        PageSize = 24,
                        SearchTerm = "banana",
                        AlbumPath = "Madaffald/Frugt",
                        IncludeSubalbums = false
                }, cancellationToken: TestContext.Current.CancellationToken);

                // Assert
                Assert.Equal(
                        expected: "https://content.oekolariet.dk/content/api/v1/images?page=1&pageSize=24&searchTerm=banana&albumPath=Madaffald%2FFrugt&includeSubalbums=false",
                        actual: handler.LastRequest?.RequestUri?.AbsoluteUri);
        }

        /// <summary>
        /// Verifies <see cref="ContentServiceImageClient.ListAlbumsAsync"/> requests <c>api/images/albums</c> with encoded parent path.
        /// </summary>
        [Fact]
        public async Task ListAlbumsAsync_WithParentPath_RequestsExpectedRouteAsync()
        {
                // Arrange
                IReadOnlyList<string> expected = ["Madaffald/Frugt", "Madaffald/Grønt"];
                StubHttpMessageHandler handler = new(responder: (_, _) => Task.FromResult(result: new HttpResponseMessage(statusCode: HttpStatusCode.OK)
                {
                        Content = new StringContent(
                                content: JsonSerializer.Serialize(value: expected),
                                encoding: Encoding.UTF8,
                                mediaType: "application/json")
                }));
                ContentServiceImageClient client = CreateClient(handler: handler);

                // Act
                IReadOnlyList<string> actual = await client.ListAlbumsAsync(parentPath: "Madaffald", cancellationToken: TestContext.Current.CancellationToken);

                // Assert
                Assert.Equal(expected: expected, actual: actual);
                Assert.Equal(
                        expected: "https://content.oekolariet.dk/content/api/v1/images/albums?parentPath=Madaffald",
                        actual: handler.LastRequest?.RequestUri?.AbsoluteUri);
        }

        /// <summary>
        /// Verifies <see cref="ContentServiceImageClient.UpdateImageMetadataAsync"/> sends <c>PATCH</c> JSON and returns updated metadata.
        /// </summary>
        [Fact]
        public async Task UpdateImageMetadataAsync_WhenValid_SendsPatchAndDeserializesUpdatedMetadataAsync()
        {
                // Arrange
                Guid imageId = Guid.NewGuid();
                Guid originalStamp = Guid.NewGuid();
                Guid rotatedStamp = Guid.NewGuid();
                ContentImageResponse updated = new()
                {
                        Id = imageId,
                        PictureReference = $"picture.node-1.{imageId:D}",
                        ExternalId = $"node-1_{imageId:D}",
                        FileName = "banana.png",
                        ContentType = "image/png",
                        SizeBytes = 100,
                        Sha256Hash = new string(c: 'b', count: 64),
                        AltText = "Yellow banana",
                        AlbumPath = "Madaffald",
                        Tags = ["banana", "fruit"],
                        CreatedAt = DateTimeOffset.UtcNow,
                        ConcurrencyStamp = rotatedStamp
                };

                StubHttpMessageHandler handler = new(responder: (_, _) => Task.FromResult(result: new HttpResponseMessage(statusCode: HttpStatusCode.OK)
                {
                        Content = new StringContent(
                                content: JsonSerializer.Serialize(value: updated),
                                encoding: Encoding.UTF8,
                                mediaType: "application/json")
                }));
                ContentServiceImageClient client = CreateClient(handler: handler);

                // Act
                ContentImageResponse actual = await client.UpdateImageMetadataAsync(imageId: imageId, command: new ContentUpdateImageMetadataCommand
                {
                        AltText = "Yellow banana",
                        Tags = "banana, fruit",
                        AlbumPath = "Madaffald",
                        ExpectedConcurrencyStamp = originalStamp
                }, cancellationToken: TestContext.Current.CancellationToken);

                // Assert
                Assert.Equal(expected: HttpMethod.Patch, actual: handler.LastRequest?.Method);
                Assert.Equal(
                        expected: $"https://content.oekolariet.dk/content/api/v1/images/{imageId:D}/metadata",
                        actual: handler.LastRequest?.RequestUri?.AbsoluteUri);
                Assert.Equal(expected: rotatedStamp, actual: actual.ConcurrencyStamp);
                Assert.Equal(expected: "Madaffald", actual: actual.AlbumPath);
        }

        private static ContentServiceImageClient CreateClient(StubHttpMessageHandler handler)
        {
                Uri baseAddress = new(uriString: BASE_URL, uriKind: UriKind.Absolute);
                HttpClient httpClient = new(handler: handler) { BaseAddress = baseAddress };
                return new ContentServiceImageClient(
                        httpClient: httpClient,
                        options: Microsoft.Extensions.Options.Options.Create(options: new ContentServiceClientOptions
                        {
                                BaseAddress = baseAddress,
                                DefaultActorId = DEFAULT_ACTOR_ID
                        }));
        }
}

