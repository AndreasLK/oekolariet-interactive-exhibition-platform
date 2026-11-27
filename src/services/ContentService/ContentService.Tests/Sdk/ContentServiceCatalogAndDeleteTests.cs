using System.Net;
using System.Text;
using System.Text.Json;
using ContentService.Sdk.Clients;
using ContentService.Sdk.Exceptions;
using ContentService.Sdk.Models;
using ContentService.Sdk.Options;
using ContentService.Tests.Sdk.TestDoubles;

namespace ContentService.Tests.Sdk;

/// <summary>
/// Unit tests verifying catalog listing and soft-deletion calls on <see cref="ContentServiceImageClient"/>.
/// </summary>
public sealed class ContentServiceCatalogAndDeleteTests
{
        private const string BASE_URL_WITH_SUBPATH = "https://content.oekolariet.dk/content";
        private const string DEFAULT_ACTOR_ID = "gallery-tester";
        private const string ACTOR_HEADER_NAME = "X-Actor-Id";

        /// <summary>
        /// Verifies that <see cref="ContentServiceImageClient.ListImagesAsync"/> builds the expected query string and deserializes the page payload.
        /// </summary>
        [Fact]
        public async Task ListImagesAsync_WithPagingAndSearch_RequestsExpectedQueryAndDeserializesPageAsync()
        {
                // Arrange
                Guid imageId = Guid.NewGuid();
                ContentPagedResponse<ContentImageResponse> expected = new()
                {
                        Items =
                        [
                                new ContentImageResponse
                                {
                                        Id = imageId,
                                        PictureReference = $"picture.node-1.{imageId:D}",
                                        ExternalId = $"node-1_{imageId:D}",
                                        FileName = "ocean exhibit.png",
                                        ContentType = "image/png",
                                        SizeBytes = 128,
                                        Sha256Hash = new string(c: 'a', count: 64),
                                        AltText = "Ocean",
                                        CreatedAt = DateTimeOffset.UtcNow,
                                        ConcurrencyStamp = Guid.NewGuid()
                                }
                        ],
                        Page = 2,
                        PageSize = 12,
                        TotalCount = 15
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
                ContentPagedResponse<ContentImageResponse> actual = await client.ListImagesAsync(page: 2, pageSize: 12, searchTerm: "  ocean exhibit  ", cancellationToken: TestContext.Current.CancellationToken);

                // Assert
                Assert.Equal(expected: expected.TotalCount, actual: actual.TotalCount);
                Assert.Single(collection: actual.Items);
                Assert.Equal(expected: imageId, actual: actual.Items[0].Id);
                Assert.Equal(
                        expected: "https://content.oekolariet.dk/content/api/v1/images?page=2&pageSize=12&searchTerm=ocean%20exhibit",
                        actual: handler.LastRequest?.RequestUri?.AbsoluteUri);
        }

        /// <summary>
        /// Verifies that <see cref="ContentServiceImageClient.DeleteImageAsync"/> sends <c>DELETE</c> with actor attribution and maps conflict responses.
        /// </summary>
        [Fact]
        public async Task DeleteImageAsync_WhenSuccessOrConflict_SendsActorHeaderOrThrowsApiExceptionAsync()
        {
                // Arrange
                Guid imageId = Guid.NewGuid();
                StubHttpMessageHandler successHandler = new(responder: (_, _) => Task.FromResult(result: new HttpResponseMessage(statusCode: HttpStatusCode.NoContent)));
                ContentServiceImageClient successClient = CreateClient(handler: successHandler);

                // Act
                await successClient.DeleteImageAsync(imageId: imageId, cancellationToken: TestContext.Current.CancellationToken);

                // Assert
                Assert.Equal(expected: HttpMethod.Delete, actual: successHandler.LastRequest?.Method);
                Assert.Equal(
                        expected: $"https://content.oekolariet.dk/content/api/v1/images/{imageId:D}",
                        actual: successHandler.LastRequest?.RequestUri?.AbsoluteUri);
                Assert.True(
                        condition: successHandler.LastRequest?.Headers.TryGetValues(
                                name: ACTOR_HEADER_NAME,
                                values: out IEnumerable<string>? actorHeaders) == true
                        && actorHeaders is not null
                        && actorHeaders.Contains(value: DEFAULT_ACTOR_ID));

                // Arrange conflict response
                StubHttpMessageHandler conflictHandler = new(responder: (_, _) => Task.FromResult(result: new HttpResponseMessage(statusCode: HttpStatusCode.Conflict)
                {
                        Content = new StringContent(
                                content: """{"title":"Entity in use","status":409,"detail":"Image is referenced by a template."}""",
                                encoding: Encoding.UTF8,
                                mediaType: "application/problem+json")
                }));
                ContentServiceImageClient conflictClient = CreateClient(handler: conflictHandler);

                // Act & Assert
                ContentServiceApiException exception = await Assert.ThrowsAsync<ContentServiceApiException>(
                        testCode: () => conflictClient.DeleteImageAsync(imageId: imageId, cancellationToken: TestContext.Current.CancellationToken));
                Assert.Equal(expected: HttpStatusCode.Conflict, actual: exception.StatusCode);
        }

        private static ContentServiceImageClient CreateClient(StubHttpMessageHandler handler)
        {
                Uri baseAddress = new(uriString: BASE_URL_WITH_SUBPATH, uriKind: UriKind.Absolute);
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
