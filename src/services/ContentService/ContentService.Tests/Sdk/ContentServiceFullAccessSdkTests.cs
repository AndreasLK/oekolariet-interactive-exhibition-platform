using System.Net;
using System.Text;
using System.Text.Json;
using ContentService.Sdk;
using ContentService.Sdk.Clients;
using ContentService.Sdk.Models;
using ContentService.Sdk.Options;
using ContentService.Tests.Sdk.TestDoubles;
using Microsoft.Extensions.DependencyInjection;

namespace ContentService.Tests.Sdk;

/// <summary>
/// Unit tests verifying batch image metadata, preview downloads, shard-aware identifiers,
/// template lifecycle, platform bundle/health/pool operations, and DI registration in <c>ContentService.Sdk</c>.
/// </summary>
public sealed class ContentServiceFullAccessSdkTests
{
        private const string BASE_URL = "https://content.oekolariet.dk";
        private const string DEFAULT_ACTOR_ID = "editor-sdk-test";

        /// <summary>
        /// Verifies that <see cref="DependencyInjection.AddContentServiceSdk(IServiceCollection, Action{ContentServiceClientOptions})"/>
        /// registers image, template, and platform clients.
        /// </summary>
        [Fact]
        public void AddContentServiceSdk_WhenConfigured_ResolvesAllThreeClients()
        {
                ServiceCollection services = new();
                services.AddContentServiceSdk(configureOptions: options =>
                {
                        options.BaseAddress = new Uri(uriString: BASE_URL, uriKind: UriKind.Absolute);
                        options.DefaultActorId = DEFAULT_ACTOR_ID;
                });

                using ServiceProvider provider = services.BuildServiceProvider();

                Assert.NotNull(provider.GetRequiredService<IContentServiceImageClient>());
                Assert.NotNull(provider.GetRequiredService<IContentServiceTemplateClient>());
                Assert.NotNull(provider.GetRequiredService<IContentServicePlatformClient>());
        }

        /// <summary>
        /// Verifies that <see cref="DependencyInjection.AddContentServiceClient(IServiceCollection, Action{ContentServiceClientOptions})"/>
        /// registers all three clients and maps Refit HTTP failures into <see cref="ContentService.Sdk.Exceptions.ContentServiceApiException"/>.
        /// </summary>
        [Fact]
        public async Task AddContentServiceClient_WhenApiReturnsConflict_MapsToContentServiceApiExceptionAsync()
        {
                StubHttpMessageHandler handler = new(responder: (_, _) => Task.FromResult(new HttpResponseMessage(statusCode: HttpStatusCode.Conflict)
                {
                        Content = new StringContent(
                                content: "{\"title\":\"Conflict\",\"status\":409,\"detail\":\"Concurrency mismatch\"}",
                                encoding: Encoding.UTF8,
                                mediaType: "application/problem+json")
                }));

                ServiceCollection services = new();
                services.AddContentServiceClient(configureOptions: options =>
                {
                        options.BaseAddress = new Uri(uriString: BASE_URL, uriKind: UriKind.Absolute);
                        options.DefaultActorId = DEFAULT_ACTOR_ID;
                });

                // Wire the stub handler onto the registered Refit endpoint clients to test the full DI + Refit ExceptionFactory path.
                services.ConfigureHttpClientDefaults(configure: builder =>
                        builder.ConfigurePrimaryHttpMessageHandler(() => handler));

                using ServiceProvider provider = services.BuildServiceProvider();
                Assert.NotNull(provider.GetRequiredService<IContentServiceImageClient>());
                Assert.NotNull(provider.GetRequiredService<IContentServicePlatformClient>());

                IContentServiceTemplateClient templateClient = provider.GetRequiredService<IContentServiceTemplateClient>();
                ContentService.Sdk.Exceptions.ContentServiceApiException mapped = await Assert.ThrowsAsync<ContentService.Sdk.Exceptions.ContentServiceApiException>(
                        testCode: () => templateClient.GetTemplateAsync(
                                templateId: Guid.NewGuid(),
                                cancellationToken: TestContext.Current.CancellationToken));

                Assert.Equal(expected: HttpStatusCode.Conflict, actual: mapped.StatusCode);
                Assert.NotNull(mapped.ProblemDetails);
                Assert.Equal(expected: "Concurrency mismatch", actual: mapped.ProblemDetails!.Detail);
                Assert.Null(mapped.RawResponseBody);
        }

        /// <summary>
        /// Verifies batch metadata lookup, preview URI/download, and shard-aware <c>{node}_{guid}</c> routing on <see cref="ContentServiceImageClient"/>.
        /// </summary>
        [Fact]
        public async Task ImageClient_WhenUsingBatchPreviewAndExternalId_CallsExpectedRoutesAsync()
        {
                Guid imageId = Guid.NewGuid();
                string externalId = $"node-2_{imageId:D}";
                ContentImageResponse expectedMetadata = CreateSampleImageResponse(imageId: imageId, externalId: externalId);

                List<Uri?> capturedUris = [];
                StubHttpMessageHandler handler = new(responder: (request, _) =>
                {
                        capturedUris.Add(request.RequestUri);
                        string absolutePath = request.RequestUri?.AbsolutePath ?? string.Empty;

                        if (absolutePath.EndsWith("/batch-metadata", StringComparison.Ordinal))
                        {
                                return Task.FromResult(CreateJsonResponse(payload: new[] { expectedMetadata }));
                        }

                        if (absolutePath.EndsWith("/preview", StringComparison.Ordinal))
                        {
                                return Task.FromResult(new HttpResponseMessage(statusCode: HttpStatusCode.OK)
                                {
                                        Content = new ByteArrayContent(content: [1, 2, 3])
                                });
                        }

                        return Task.FromResult(CreateJsonResponse(payload: expectedMetadata));
                });

                ContentServiceImageClient client = CreateImageClient(handler: handler);

                IReadOnlyList<ContentImageResponse> batch = await client.GetImagesMetadataBatchAsync(
                        imageIds: [imageId],
                        cancellationToken: TestContext.Current.CancellationToken);
                Assert.Single(batch);

                ContentImageResponse byExternalId = await client.GetImageMetadataAsync(
                        imageIdentifier: externalId,
                        cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(expected: externalId, actual: byExternalId.ExternalId);

                using ContentImageDownloadResponse preview = await client.DownloadImagePreviewAsync(
                        imageIdentifier: externalId,
                        cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(
                        expected: $"{BASE_URL}/api/v1/images/{externalId}/preview",
                        actual: client.GetImagePreviewUri(imageIdentifier: externalId).AbsoluteUri);

                Assert.Equal(expected: $"{BASE_URL}/api/v1/images/batch-metadata", actual: capturedUris[0]?.AbsoluteUri);
                Assert.Equal(expected: $"{BASE_URL}/api/v1/images/{externalId}", actual: capturedUris[1]?.AbsoluteUri);
                Assert.Equal(expected: $"{BASE_URL}/api/v1/images/{externalId}/preview", actual: capturedUris[2]?.AbsoluteUri);
        }

        /// <summary>
        /// Verifies template CRUD and image-reference checks on <see cref="ContentServiceTemplateClient"/>.
        /// </summary>
        [Fact]
        public async Task TemplateClient_WhenInvokingLifecycleAndReferenceCheck_CallsExpectedRoutesAsync()
        {
                Guid templateId = Guid.NewGuid();
                Guid imageId = Guid.NewGuid();
                ContentTemplateResponse expectedTemplate = CreateSampleTemplateResponse(templateId: templateId);

                List<(HttpMethod Method, string? Uri, string? Actor)> requests = [];
                StubHttpMessageHandler handler = new(responder: (request, _) =>
                {
                        string? actor = request.Headers.TryGetValues(name: "X-Actor-Id", values: out IEnumerable<string>? values)
                                ? values.FirstOrDefault()
                                : null;
                        requests.Add((request.Method, request.RequestUri?.AbsoluteUri, actor));

                        if (request.RequestUri?.AbsolutePath.Contains("/image-references/", StringComparison.Ordinal) == true)
                        {
                                return Task.FromResult(CreateJsonResponse(payload: new ContentImageTemplateReferenceResponse(IsReferenced: true)));
                        }

                        if (request.Method == HttpMethod.Get && request.RequestUri?.Query.Length > 0)
                        {
                                ContentPagedResponse<ContentTemplateResponse> page = new()
                                {
                                        Items = [expectedTemplate],
                                        TotalCount = 1,
                                        Page = 1,
                                        PageSize = 25
                                };
                                return Task.FromResult(CreateJsonResponse(payload: page));
                        }

                        if (request.Method == HttpMethod.Delete)
                        {
                                return Task.FromResult(new HttpResponseMessage(statusCode: HttpStatusCode.NoContent));
                        }

                        return Task.FromResult(CreateJsonResponse(payload: expectedTemplate));
                });

                Uri baseAddress = new(uriString: BASE_URL, uriKind: UriKind.Absolute);
                ContentServiceTemplateClient client = new(
                        httpClient: new HttpClient(handler: handler) { BaseAddress = baseAddress },
                        options: Microsoft.Extensions.Options.Options.Create(options: new ContentServiceClientOptions
                        {
                                BaseAddress = baseAddress,
                                DefaultActorId = DEFAULT_ACTOR_ID
                        }));

                ContentTemplateResponse created = await client.CreateTemplateAsync(
                        command: new ContentCreateTemplateCommand(
                                Name: "Quiz Layout",
                                ComponentType: ContentTemplateComponentType.QuizTemplate,
                                SchemaJson: "{}"),
                        cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(expected: templateId, actual: created.Id);

                ContentPagedResponse<ContentTemplateResponse> listed = await client.ListTemplatesAsync(
                        componentType: ContentTemplateComponentType.QuizTemplate,
                        searchTerm: "Quiz",
                        cancellationToken: TestContext.Current.CancellationToken);
                Assert.Single(listed.Items);

                ContentTemplateResponse fetched = await client.GetTemplateAsync(
                        templateId: templateId,
                        cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(expected: expectedTemplate.Name, actual: fetched.Name);

                ContentTemplateResponse updated = await client.UpdateTemplateAsync(
                        templateId: templateId,
                        command: new ContentUpdateTemplateCommand(
                                Name: "Quiz Layout v2",
                                SchemaJson: "{\"v\":2}",
                                ExpectedConcurrencyStamp: expectedTemplate.ConcurrencyStamp),
                        cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(expected: templateId, actual: updated.Id);

                ContentImageTemplateReferenceResponse referenceCheck = await client.IsImageReferencedByTemplateAsync(
                        imageId: imageId,
                        cancellationToken: TestContext.Current.CancellationToken);
                Assert.True(referenceCheck.IsReferenced);

                await client.DeleteTemplateAsync(
                        templateId: templateId,
                        cancellationToken: TestContext.Current.CancellationToken);

                Assert.Equal(expected: 6, actual: requests.Count);
                Assert.Equal(expected: DEFAULT_ACTOR_ID, actual: requests[0].Actor);
                Assert.Equal(expected: DEFAULT_ACTOR_ID, actual: requests[3].Actor);
                Assert.Equal(expected: DEFAULT_ACTOR_ID, actual: requests[5].Actor);
                Assert.Equal(expected: $"{BASE_URL}/api/v1/templates/image-references/{imageId:D}", actual: requests[4].Uri);
        }

        /// <summary>
        /// Verifies publish bundle resolution, storage health, and pool upload-offer requests on <see cref="ContentServicePlatformClient"/>.
        /// </summary>
        [Fact]
        public async Task PlatformClient_WhenResolvingBundleHealthAndPoolOffer_DeserializesResponsesAsync()
        {
                Guid imageId = Guid.NewGuid();
                ContentPublishBundleResponse expectedBundle = new()
                {
                        TemplateId = null,
                        TemplateSchemaJson = null,
                        ImplementationJson = "{\"title\":\"Waste Quiz\"}",
                        Assets =
                        [
                                new ContentBundleAssetResponse
                                {
                                        ImageId = imageId,
                                        PictureReference = $"picture.node-1.{imageId:D}",
                                        ExternalId = $"node-1_{imageId:D}",
                                        ContentType = "image/png",
                                        SizeBytes = 128,
                                        Sha256Hash = new string(c: 'a', count: 64)
                                }
                        ],
                        BundleChecksum = new string(c: 'c', count: 64)
                };

                ContentStorageHealthResponse expectedHealth = new()
                {
                        Status = ContentStorageHealthStatus.Healthy,
                        UsedBytes = 1024,
                        QuotaBytes = 4096,
                        UtilizationPercent = 25.0
                };

                ContentPoolNodeOfferResponse expectedOffer = new()
                {
                        StorageNodeId = "node-1",
                        DaprAppId = "contentservice-node-1",
                        PublicBaseUrl = BASE_URL,
                        AvailableBytes = 3072
                };

                StubHttpMessageHandler handler = new(responder: (request, _) =>
                {
                        string path = request.RequestUri?.AbsolutePath ?? string.Empty;
                        if (path.EndsWith("/publish-bundles/resolve", StringComparison.Ordinal))
                        {
                                return Task.FromResult(CreateJsonResponse(payload: expectedBundle));
                        }

                        if (path.EndsWith("/health/storage", StringComparison.Ordinal))
                        {
                                return Task.FromResult(CreateJsonResponse(payload: expectedHealth));
                        }

                        return Task.FromResult(CreateJsonResponse(payload: expectedOffer));
                });

                Uri baseAddress = new(uriString: BASE_URL, uriKind: UriKind.Absolute);
                ContentServicePlatformClient client = new(
                        httpClient: new HttpClient(handler: handler) { BaseAddress = baseAddress },
                        options: Microsoft.Extensions.Options.Options.Create(options: new ContentServiceClientOptions
                        {
                                BaseAddress = baseAddress
                        }));

                ContentPublishBundleResponse bundle = await client.ResolvePublishBundleAsync(
                        query: new ContentResolvePublishBundleQuery { ImplementationJson = "{\"title\":\"Waste Quiz\"}" },
                        cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(expected: expectedBundle.BundleChecksum, actual: bundle.BundleChecksum);

                ContentStorageHealthResponse health = await client.GetStorageHealthAsync(
                        cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(expected: ContentStorageHealthStatus.Healthy, actual: health.Status);

                ContentPoolNodeOfferResponse offer = await client.RequestUploadOfferAsync(
                        command: new ContentRequestUploadOfferCommand(RequiredBytes: 1024),
                        cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(expected: "node-1", actual: offer.StorageNodeId);
        }

        private static ContentServiceImageClient CreateImageClient(StubHttpMessageHandler handler)
        {
                Uri baseAddress = new(uriString: BASE_URL, uriKind: UriKind.Absolute);
                return new ContentServiceImageClient(
                        httpClient: new HttpClient(handler: handler) { BaseAddress = baseAddress },
                        options: Microsoft.Extensions.Options.Options.Create(options: new ContentServiceClientOptions
                        {
                                BaseAddress = baseAddress,
                                DefaultActorId = DEFAULT_ACTOR_ID
                        }));
        }

        private static HttpResponseMessage CreateJsonResponse<T>(T payload) =>
                new(statusCode: HttpStatusCode.OK)
                {
                        Content = new StringContent(
                                content: JsonSerializer.Serialize(value: payload),
                                encoding: Encoding.UTF8,
                                mediaType: "application/json")
                };

        private static ContentImageResponse CreateSampleImageResponse(Guid imageId, string externalId) =>
                new()
                {
                        Id = imageId,
                        PictureReference = $"picture.node-2.{imageId:D}",
                        ExternalId = externalId,
                        FileName = "sample.png",
                        ContentType = "image/png",
                        SizeBytes = 128,
                        Sha256Hash = new string(c: 'b', count: 64),
                        CreatedAt = DateTimeOffset.UtcNow,
                        ConcurrencyStamp = Guid.NewGuid()
                };

        private static ContentTemplateResponse CreateSampleTemplateResponse(Guid templateId) =>
                new()
                {
                        Id = templateId,
                        ExternalId = $"node-1_{templateId:D}",
                        Name = "Quiz Layout",
                        ComponentType = ContentTemplateComponentType.QuizTemplate,
                        SchemaJson = "{}",
                        SchemaVersion = 1,
                        IsArchived = false,
                        ReferencedPictureReferences = [],
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow,
                        ConcurrencyStamp = Guid.NewGuid()
                };
}
