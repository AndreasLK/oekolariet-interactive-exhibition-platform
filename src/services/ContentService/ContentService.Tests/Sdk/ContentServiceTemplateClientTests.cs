using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ContentService.Sdk;
using ContentService.Sdk.Clients;
using ContentService.Sdk.Exceptions;
using ContentService.Sdk.Models;
using ContentService.Sdk.Options;
using ContentService.Tests.Sdk.TestDoubles;
using Microsoft.Extensions.DependencyInjection;

namespace ContentService.Tests.Sdk;

/// <summary>
/// Unit tests verifying template CRUD, query string construction, actor attribution, and DI registration for <see cref="ContentServiceTemplateClient"/>.
/// </summary>
public sealed class ContentServiceTemplateClientTests
{
        private const string BASE_URL_WITH_SUBPATH = "https://content.oekolariet.dk/content";
        private const string DEFAULT_ACTOR_ID = "editor-service";
        private const string ACTOR_HEADER_NAME = "X-Actor-Id";

        private static readonly JsonSerializerOptions WEB_JSON_OPTIONS = CreateWebJsonOptions();

        /// <summary>
        /// Verifies that <see cref="ContentServiceTemplateClient.ListTemplatesAsync(int, int, ContentTemplateComponentType?, string?, CancellationToken)"/> builds the expected query string and deserializes the page payload.
        /// </summary>
        [Fact]
        public async Task ListTemplatesAsync_WithFilters_RequestsExpectedQueryAndDeserializesPageAsync()
        {
                // Arrange
                Guid templateId = Guid.NewGuid();
                ContentTemplateMetadata expectedItem = CreateSampleMetadata(templateId: templateId);
                ContentPagedResult<ContentTemplateMetadata> expected = new()
                {
                        Items = [expectedItem],
                        Page = 2,
                        PageSize = 10,
                        TotalCount = 12
                };

                StubHttpMessageHandler handler = new(responder: (_, _) => Task.FromResult(result: CreateJsonResponse(statusCode: HttpStatusCode.OK, payload: expected)));
                ContentServiceTemplateClient client = CreateClient(handler: handler);

                // Act
                ContentPagedResult<ContentTemplateMetadata> actual = await client.ListTemplatesAsync(page: 2, pageSize: 10, componentType: ContentTemplateComponentType.QuizTemplate, searchTerm: "  climate quiz  ", cancellationToken: TestContext.Current.CancellationToken);

                // Assert
                Assert.Equal(expected: expected.TotalCount, actual: actual.TotalCount);
                Assert.Single(collection: actual.Items);
                Assert.Equal(expected: expectedItem, actual: actual.Items[0]);
                Assert.Equal(
                        expected: "https://content.oekolariet.dk/content/api/templates?page=2&pageSize=10&componentType=QuizTemplate&searchTerm=climate%20quiz",
                        actual: handler.LastRequest?.RequestUri?.AbsoluteUri);
        }

        /// <summary>
        /// Verifies that <see cref="ContentServiceTemplateClient.GetTemplateAsync"/> calls <c>GET /api/templates/{id}</c> and deserializes the template metadata.
        /// </summary>
        [Fact]
        public async Task GetTemplateAsync_WithValidId_RequestsExpectedUriAndDeserializesTemplateAsync()
        {
                // Arrange
                Guid templateId = Guid.NewGuid();
                ContentTemplateMetadata expected = CreateSampleMetadata(templateId: templateId);
                StubHttpMessageHandler handler = new(responder: (_, _) => Task.FromResult(result: CreateJsonResponse(statusCode: HttpStatusCode.OK, payload: expected)));
                ContentServiceTemplateClient client = CreateClient(handler: handler);

                // Act
                ContentTemplateMetadata actual = await client.GetTemplateAsync(templateId: templateId, cancellationToken: TestContext.Current.CancellationToken);

                // Assert
                Assert.Equal(expected: expected, actual: actual);
                Assert.Equal(expected: HttpMethod.Get, actual: handler.LastRequest?.Method);
                Assert.Equal(
                        expected: $"https://content.oekolariet.dk/content/api/templates/{templateId:D}",
                        actual: handler.LastRequest?.RequestUri?.AbsoluteUri);
        }

        /// <summary>
        /// Verifies that <see cref="ContentServiceTemplateClient.CreateTemplateAsync"/> sends <c>POST /api/templates</c> with actor header and JSON body.
        /// </summary>
        [Fact]
        public async Task CreateTemplateAsync_WithValidRequest_SendsPostWithActorHeaderAndBodyAsync()
        {
                // Arrange
                Guid templateId = Guid.NewGuid();
                ContentTemplateMetadata expected = CreateSampleMetadata(templateId: templateId);
                StubHttpMessageHandler handler = new(responder: (_, _) => Task.FromResult(result: CreateJsonResponse(statusCode: HttpStatusCode.Created, payload: expected)));
                ContentServiceTemplateClient client = CreateClient(handler: handler);

                CreateContentTemplateRequest request = new()
                {
                        Name = "Climate Quiz",
                        ComponentType = ContentTemplateComponentType.QuizTemplate,
                        SchemaJson = "{\"title\":\"Climate\"}"
                };

                // Act
                ContentTemplateMetadata actual = await client.CreateTemplateAsync(request: request, cancellationToken: TestContext.Current.CancellationToken);

                // Assert
                Assert.Equal(expected: expected, actual: actual);
                Assert.Equal(expected: HttpMethod.Post, actual: handler.LastRequest?.Method);
                Assert.Equal(expected: "https://content.oekolariet.dk/content/api/templates", actual: handler.LastRequest?.RequestUri?.AbsoluteUri);
                Assert.Equal(expected: DEFAULT_ACTOR_ID, actual: handler.LastRequest?.Headers.GetValues(ACTOR_HEADER_NAME).Single());
        }

        /// <summary>
        /// Verifies that <see cref="ContentServiceTemplateClient.UpdateTemplateAsync"/> sends <c>PUT /api/templates/{id}</c> and maps 409 Conflict to <see cref="ContentServiceApiException"/>.
        /// </summary>
        [Fact]
        public async Task UpdateTemplateAsync_WhenConflictReturned_ThrowsContentServiceApiExceptionAsync()
        {
                // Arrange
                Guid templateId = Guid.NewGuid();
                StubHttpMessageHandler handler = new(responder: (_, _) => Task.FromResult(result: new HttpResponseMessage(statusCode: HttpStatusCode.Conflict)
                {
                        Content = new StringContent(
                                content: "{\"title\":\"Concurrency conflict\",\"status\":409,\"detail\":\"Stale concurrency stamp.\"}",
                                encoding: Encoding.UTF8,
                                mediaType: "application/problem+json")
                }));
                ContentServiceTemplateClient client = CreateClient(handler: handler);

                UpdateContentTemplateRequest request = new()
                {
                        Name = "Updated Quiz",
                        SchemaJson = "{\"title\":\"Updated\"}",
                        ExpectedConcurrencyStamp = Guid.NewGuid(),
                        ActorId = "custom-actor"
                };

                // Act & Assert
                ContentServiceApiException ex = await Assert.ThrowsAsync<ContentServiceApiException>(
                        testCode: () => client.UpdateTemplateAsync(templateId: templateId, request: request, cancellationToken: TestContext.Current.CancellationToken));

                Assert.Equal(expected: HttpStatusCode.Conflict, actual: ex.StatusCode);
                Assert.Equal(expected: HttpMethod.Put, actual: handler.LastRequest?.Method);
                Assert.Equal(expected: "custom-actor", actual: handler.LastRequest?.Headers.GetValues(ACTOR_HEADER_NAME).Single());
        }

        /// <summary>
        /// Verifies that <see cref="ContentServiceTemplateClient.DeleteTemplateAsync"/> sends <c>DELETE /api/templates/{id}</c> with actor attribution.
        /// </summary>
        [Fact]
        public async Task DeleteTemplateAsync_WithValidId_SendsDeleteWithActorHeaderAsync()
        {
                // Arrange
                Guid templateId = Guid.NewGuid();
                StubHttpMessageHandler handler = new(responder: (_, _) => Task.FromResult(result: new HttpResponseMessage(statusCode: HttpStatusCode.NoContent)));
                ContentServiceTemplateClient client = CreateClient(handler: handler);

                // Act
                await client.DeleteTemplateAsync(templateId: templateId, cancellationToken: TestContext.Current.CancellationToken);

                // Assert
                Assert.Equal(expected: HttpMethod.Delete, actual: handler.LastRequest?.Method);
                Assert.Equal(
                        expected: $"https://content.oekolariet.dk/content/api/templates/{templateId:D}",
                        actual: handler.LastRequest?.RequestUri?.AbsoluteUri);
                Assert.Equal(expected: DEFAULT_ACTOR_ID, actual: handler.LastRequest?.Headers.GetValues(ACTOR_HEADER_NAME).Single());
        }

        /// <summary>
        /// Verifies that <see cref="ContentServiceTemplateClient.GetTemplateAsync"/> rejects <see cref="Guid.Empty"/> before making an HTTP request.
        /// </summary>
        [Fact]
        public async Task GetTemplateAsync_WithEmptyGuid_ThrowsArgumentExceptionAsync()
        {
                // Arrange
                StubHttpMessageHandler handler = new(responder: (_, _) => Task.FromResult(result: new HttpResponseMessage(statusCode: HttpStatusCode.OK)));
                ContentServiceTemplateClient client = CreateClient(handler: handler);

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentException>(testCode: () => client.GetTemplateAsync(templateId: Guid.Empty, cancellationToken: TestContext.Current.CancellationToken));
                Assert.Null(handler.LastRequest);
        }

        /// <summary>
        /// Verifies that <see cref="DependencyInjection.AddContentServiceSdk(IServiceCollection, Action{ContentServiceClientOptions})"/> registers <see cref="IContentServiceTemplateClient"/>.
        /// </summary>
        [Fact]
        public void AddContentServiceSdk_RegistersTemplateClient()
        {
                // Arrange
                ServiceCollection services = new();
                services.AddContentServiceSdk(options =>
                {
                        options.BaseAddress = new Uri(uriString: BASE_URL_WITH_SUBPATH, uriKind: UriKind.Absolute);
                });

                using ServiceProvider provider = services.BuildServiceProvider();

                // Act
                IContentServiceTemplateClient? templateClient = provider.GetService<IContentServiceTemplateClient>();

                // Assert
                Assert.NotNull(templateClient);
        }

        private static ContentServiceTemplateClient CreateClient(StubHttpMessageHandler handler)
        {
                HttpClient httpClient = new(handler: handler)
                {
                        BaseAddress = new Uri(uriString: BASE_URL_WITH_SUBPATH, uriKind: UriKind.Absolute)
                };

                ContentServiceClientOptions options = new()
                {
                        BaseAddress = new Uri(uriString: BASE_URL_WITH_SUBPATH, uriKind: UriKind.Absolute),
                        DefaultActorId = DEFAULT_ACTOR_ID
                };

                return new ContentServiceTemplateClient(
                        httpClient: httpClient,
                        options: Microsoft.Extensions.Options.Options.Create(options: options));
        }

        private static ContentTemplateMetadata CreateSampleMetadata(Guid templateId) =>
                new()
                {
                        Id = templateId,
                        ExternalId = $"node-1_{templateId:D}",
                        Name = "Climate Quiz",
                        ComponentType = ContentTemplateComponentType.QuizTemplate,
                        SchemaJson = "{\"title\":\"Climate\"}",
                        SchemaVersion = 1,
                        IsArchived = false,
                        ReferencedPictureReferences = [$"picture.node-1.{Guid.NewGuid():D}"],
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow,
                        ConcurrencyStamp = Guid.NewGuid()
                };

        private static HttpResponseMessage CreateJsonResponse<T>(HttpStatusCode statusCode, T payload) =>
                new(statusCode: statusCode)
                {
                        Content = new StringContent(
                                content: JsonSerializer.Serialize(value: payload, options: WEB_JSON_OPTIONS),
                                encoding: Encoding.UTF8,
                                mediaType: "application/json")
                };

        private static JsonSerializerOptions CreateWebJsonOptions()
        {
                JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
                options.Converters.Add(new JsonStringEnumConverter());
                return options;
        }
}
