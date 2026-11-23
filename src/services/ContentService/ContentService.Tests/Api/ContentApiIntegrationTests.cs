using System.Net;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text;
using ContentService.Api.Contracts;
using ContentService.Application.Models;
using ContentService.Domain.Common;
using ContentService.Domain.Enums;
using ContentService.Tests.Application.Persistence;
using Microsoft.Net.Http.Headers;

namespace ContentService.Tests.Api;

/// <summary>
/// End-to-end HTTP integration tests verifying template CRUD, publishing, storage health, CORS, and pipeline error mappings against SQL Server.
/// </summary>
[Collection(MsSqlContentDatabaseCollection.COLLECTION_NAME)]
public sealed class ContentApiIntegrationTests : IAsyncLifetime
{
        private const int SMALL_MAX_IMAGE_BYTES = 64;
        private const int MULTIPART_LIMIT_EXCEEDING_BYTES = 1_200_000;

        private readonly MsSqlContentDatabaseFixture _fixture;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContentApiIntegrationTests"/> class.
        /// </summary>
        /// <param name="fixture">The shared SQL Server container fixture.</param>
        public ContentApiIntegrationTests(MsSqlContentDatabaseFixture fixture)
        {
                ArgumentNullException.ThrowIfNull(fixture);
                this._fixture = fixture;
        }

        /// <inheritdoc />
        public ValueTask InitializeAsync() => new(this._fixture.ResetDatabaseAsync());

        /// <inheritdoc />
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        /// <summary>
        /// Verifies the full HTTP template CRUD lifecycle, duplicate-name 400 validation, stale concurrency token 409 Conflict, and archive delete behavior.
        /// </summary>
        [Fact]
        public async Task TemplatesApi_CrudDuplicateConcurrencyAndArchive_ReturnsExpectedResponsesAsync()
        {
                // Arrange
                using ContentApiWebApplicationFactory factory = new(connectionString: this._fixture.ConnectionString);
                using HttpClient client = factory.CreateClient();
                client.DefaultRequestHeaders.Add(
                        name: ApiIntegrationTestFixtures.ACTOR_HEADER_NAME,
                        value: ApiIntegrationTestFixtures.TEST_ACTOR_ID);

                CreateTemplateRequest createRequest = new(
                        Name: "Water Exhibit",
                        ComponentType: ContentService.Facade.Dtos.TemplateComponentTypeDto.InfoScreen,
                        SchemaJson: "{}");

                // Act 1: Create -> 201
                using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
                        requestUri: ApiIntegrationTestFixtures.TEMPLATES_ROUTE,
                        value: createRequest,
                        options: ApiIntegrationTestFixtures.API_JSON_OPTIONS);
                Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
                TemplateResult? created = await createResponse.Content.ReadFromJsonAsync<TemplateResult>(
                        options: ApiIntegrationTestFixtures.API_JSON_OPTIONS);
                Assert.NotNull(created);
                Assert.Equal($"{ApiIntegrationTestFixtures.TEMPLATES_ROUTE}/{created.Id}", createResponse.Headers.Location?.ToString());

                // Act 2: Duplicate name -> 400 ProblemDetails
                using HttpResponseMessage duplicateResponse = await client.PostAsJsonAsync(
                        requestUri: ApiIntegrationTestFixtures.TEMPLATES_ROUTE,
                        value: createRequest with { Name = "  water exhibit " },
                        options: ApiIntegrationTestFixtures.API_JSON_OPTIONS);
                await ApiIntegrationTestFixtures.AssertProblemDetailsStatusAsync(
                        response: duplicateResponse,
                        expectedStatusCode: HttpStatusCode.BadRequest);

                // Act 3: Get & List -> 200, Missing -> 404
                using HttpResponseMessage getResponse = await client.GetAsync($"{ApiIntegrationTestFixtures.TEMPLATES_ROUTE}/{created.Id}");
                using HttpResponseMessage missingResponse = await client.GetAsync($"{ApiIntegrationTestFixtures.TEMPLATES_ROUTE}/{Guid.NewGuid()}");
                using HttpResponseMessage listResponse = await client.GetAsync($"{ApiIntegrationTestFixtures.TEMPLATES_ROUTE}?componentType=InfoScreen&searchTerm=water");
                Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
                Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

                // Act 4: Update -> 200, Stale token -> 409 ProblemDetails
                UpdateTemplateRequest updateRequest = new(
                        Name: "Water Exhibit V2",
                        SchemaJson: "{\"version\":2}",
                        ExpectedConcurrencyStamp: created.ConcurrencyStamp);
                using HttpResponseMessage updateResponse = await client.PutAsJsonAsync(
                        requestUri: $"{ApiIntegrationTestFixtures.TEMPLATES_ROUTE}/{created.Id}",
                        value: updateRequest,
                        options: ApiIntegrationTestFixtures.API_JSON_OPTIONS);
                Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
                TemplateResult? updated = await updateResponse.Content.ReadFromJsonAsync<TemplateResult>(
                        options: ApiIntegrationTestFixtures.API_JSON_OPTIONS);
                Assert.NotNull(updated);
                Assert.Equal(DomainConstants.INITIAL_VERSION + 1, updated.SchemaVersion);

                using HttpResponseMessage staleUpdateResponse = await client.PutAsJsonAsync(
                        requestUri: $"{ApiIntegrationTestFixtures.TEMPLATES_ROUTE}/{created.Id}",
                        value: updateRequest,
                        options: ApiIntegrationTestFixtures.API_JSON_OPTIONS);
                await ApiIntegrationTestFixtures.AssertProblemDetailsStatusAsync(
                        response: staleUpdateResponse,
                        expectedStatusCode: HttpStatusCode.Conflict);

                // Act 5: Delete (archive) -> 204, archived GET -> 200 (IsArchived == true), second delete -> 404
                using HttpResponseMessage deleteResponse = await client.DeleteAsync($"{ApiIntegrationTestFixtures.TEMPLATES_ROUTE}/{created.Id}");
                using HttpResponseMessage archivedGetResponse = await client.GetAsync($"{ApiIntegrationTestFixtures.TEMPLATES_ROUTE}/{created.Id}");
                using HttpResponseMessage secondDeleteResponse = await client.DeleteAsync($"{ApiIntegrationTestFixtures.TEMPLATES_ROUTE}/{created.Id}");
                Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
                Assert.Equal(HttpStatusCode.OK, archivedGetResponse.StatusCode);
                TemplateResult? archivedTemplate = await archivedGetResponse.Content.ReadFromJsonAsync<TemplateResult>(
                        options: ApiIntegrationTestFixtures.API_JSON_OPTIONS);
                Assert.NotNull(archivedTemplate);
                Assert.True(archivedTemplate.IsArchived);
                Assert.Equal(HttpStatusCode.NotFound, secondDeleteResponse.StatusCode);
        }

        /// <summary>
        /// Verifies publish bundle resolution (200 deterministic checksum), missing template/image (404), malformed JSON (400), and storage health (200).
        /// </summary>
        [Fact]
        public async Task PublishingAndHealthApis_VerifyBundleChecksumErrorMappingAndHealthMetricsAsync()
        {
                // Arrange
                Dictionary<string, string?> overrides = new()
                {
                        [ApiIntegrationTestFixtures.TOTAL_QUOTA_CONFIG_KEY] = "100",
                        [ApiIntegrationTestFixtures.WARNING_THRESHOLD_CONFIG_KEY] = "10.0",
                        [ApiIntegrationTestFixtures.CRITICAL_THRESHOLD_CONFIG_KEY] = "90.0"
                };
                using ContentApiWebApplicationFactory factory = new(
                        connectionString: this._fixture.ConnectionString,
                        configurationOverrides: overrides);
                using HttpClient client = factory.CreateClient();

                // Health when empty -> Healthy, 0 bytes
                using HttpResponseMessage emptyHealthResponse = await client.GetAsync(ApiIntegrationTestFixtures.STORAGE_HEALTH_ROUTE);
                Assert.Equal(HttpStatusCode.OK, emptyHealthResponse.StatusCode);
                StorageHealthResult? emptyHealth = await emptyHealthResponse.Content.ReadFromJsonAsync<StorageHealthResult>(
                        options: ApiIntegrationTestFixtures.API_JSON_OPTIONS);
                Assert.NotNull(emptyHealth);
                Assert.Equal(StorageHealthStatus.Healthy, emptyHealth.Status);
                Assert.Equal(0L, emptyHealth.UsedBytes);

                // Upload 12-byte PNG -> 12% utilization -> Warning
                using HttpResponseMessage uploadResponse = await ApiIntegrationTestFixtures.UploadImageAsync(
                        client: client,
                        payload: ApiIntegrationTestFixtures.VALID_PNG_PAYLOAD,
                        fileName: "bundle.png",
                        contentType: DomainConstants.CONTENT_TYPE_PNG);
                ImageMetadataResult? image = await uploadResponse.Content.ReadFromJsonAsync<ImageMetadataResult>(
                        options: ApiIntegrationTestFixtures.API_JSON_OPTIONS);
                Assert.NotNull(image);

                using HttpResponseMessage warningHealthResponse = await client.GetAsync(ApiIntegrationTestFixtures.STORAGE_HEALTH_ROUTE);
                StorageHealthResult? warningHealth = await warningHealthResponse.Content.ReadFromJsonAsync<StorageHealthResult>(
                        options: ApiIntegrationTestFixtures.API_JSON_OPTIONS);
                Assert.NotNull(warningHealth);
                Assert.Equal(StorageHealthStatus.Warning, warningHealth.Status);
                Assert.Equal(ApiIntegrationTestFixtures.VALID_PNG_PAYLOAD.Length, warningHealth.UsedBytes);

                // Publish bundle -> 200
                ResolvePublishBundleCommand validBundleCommand = new()
                {
                        TemplateId = null,
                        ImplementationJson = $"{{\"bg\":\"{image.PictureReference}\"}}"
                };
                using HttpResponseMessage bundleResponse = await client.PostAsJsonAsync(
                        requestUri: ApiIntegrationTestFixtures.PUBLISH_BUNDLES_ROUTE,
                        value: validBundleCommand,
                        options: ApiIntegrationTestFixtures.API_JSON_OPTIONS);
                Assert.Equal(HttpStatusCode.OK, bundleResponse.StatusCode);
                PublishBundleResult? bundle = await bundleResponse.Content.ReadFromJsonAsync<PublishBundleResult>(
                        options: ApiIntegrationTestFixtures.API_JSON_OPTIONS);
                Assert.NotNull(bundle);
                Assert.Equal(DomainConstants.SHA256_HEX_LENGTH, bundle.BundleChecksum.Length);
                BundleAssetResult asset = Assert.Single(bundle.Assets);
                Assert.Equal(image.Id, asset.ImageId);

                // Missing template -> 404 & Missing image -> 404
                using HttpResponseMessage missingTemplateResponse = await client.PostAsJsonAsync(
                        requestUri: ApiIntegrationTestFixtures.PUBLISH_BUNDLES_ROUTE,
                        value: new ResolvePublishBundleCommand { TemplateId = Guid.NewGuid(), ImplementationJson = "{}" },
                        options: ApiIntegrationTestFixtures.API_JSON_OPTIONS);
                Assert.Equal(HttpStatusCode.NotFound, missingTemplateResponse.StatusCode);

                using HttpResponseMessage missingImageResponse = await client.PostAsJsonAsync(
                        requestUri: ApiIntegrationTestFixtures.PUBLISH_BUNDLES_ROUTE,
                        value: new ResolvePublishBundleCommand
                        {
                                TemplateId = null,
                                ImplementationJson = $"{{\"missing\":\"picture.node-1.{Guid.NewGuid():D}\"}}"
                        },
                        options: ApiIntegrationTestFixtures.API_JSON_OPTIONS);
                Assert.Equal(HttpStatusCode.NotFound, missingImageResponse.StatusCode);

                // Malformed JSON -> 400 & Base64 data URI -> 400
                using HttpResponseMessage malformedJsonResponse = await client.PostAsJsonAsync(
                        requestUri: ApiIntegrationTestFixtures.PUBLISH_BUNDLES_ROUTE,
                        value: new ResolvePublishBundleCommand { TemplateId = null, ImplementationJson = "{bad-json" },
                        options: ApiIntegrationTestFixtures.API_JSON_OPTIONS);
                Assert.Equal(HttpStatusCode.BadRequest, malformedJsonResponse.StatusCode);

                using HttpResponseMessage base64Response = await client.PostAsJsonAsync(
                        requestUri: ApiIntegrationTestFixtures.PUBLISH_BUNDLES_ROUTE,
                        value: new ResolvePublishBundleCommand
                        {
                                TemplateId = null,
                                ImplementationJson = "{\"img\":\"data:image/png;base64,AAAA\"}"
                        },
                        options: ApiIntegrationTestFixtures.API_JSON_OPTIONS);
                Assert.Equal(HttpStatusCode.BadRequest, base64Response.StatusCode);
        }

        /// <summary>
        /// Verifies CORS preflight headers, CORS preservation on error responses, and transport error mappings (400, 413, 415).
        /// </summary>
        [Fact]
        public async Task ApiPipeline_WhenCorsPreflightOrMalformedBody_HandlesCleanlyAsync()
        {
                // Arrange
                const string ALLOWED_ORIGIN = "https://localhost:5001";
                Dictionary<string, string?> corsOverrides = new()
                {
                        ["Cors:AllowedOrigins:0"] = ALLOWED_ORIGIN
                };
                using ContentApiWebApplicationFactory factory = new(
                        connectionString: this._fixture.ConnectionString,
                        configurationOverrides: corsOverrides);
                using HttpClient client = factory.CreateClient();

                // Act 1: CORS preflight
                using HttpRequestMessage preflight = new(HttpMethod.Options, ApiIntegrationTestFixtures.IMAGES_ROUTE);
                preflight.Headers.Add(HeaderNames.Origin, ALLOWED_ORIGIN);
                preflight.Headers.Add(HeaderNames.AccessControlRequestMethod, HttpMethod.Post.Method);
                using HttpResponseMessage preflightResponse = await client.SendAsync(preflight);

                // Assert 1
                Assert.Equal(HttpStatusCode.NoContent, preflightResponse.StatusCode);
                Assert.True(preflightResponse.Headers.TryGetValues(HeaderNames.AccessControlAllowOrigin, out IEnumerable<string>? origins));
                Assert.Contains(ALLOWED_ORIGIN, origins);

                // Act 2: Cross-origin malformed JSON payload on POST /api/templates -> 400 ProblemDetails WITH CORS headers preserved
                using HttpRequestMessage badCrossOriginRequest = new(HttpMethod.Post, ApiIntegrationTestFixtures.TEMPLATES_ROUTE)
                {
                        Content = new StringContent("{not-valid-json", Encoding.UTF8, MediaTypeNames.Application.Json)
                };
                badCrossOriginRequest.Headers.Add(HeaderNames.Origin, ALLOWED_ORIGIN);
                using HttpResponseMessage badBodyResponse = await client.SendAsync(badCrossOriginRequest);

                // Assert 2
                Assert.True(badBodyResponse.Headers.TryGetValues(HeaderNames.AccessControlAllowOrigin, out IEnumerable<string>? errorOrigins));
                Assert.Contains(ALLOWED_ORIGIN, errorOrigins);
                await ApiIntegrationTestFixtures.AssertProblemDetailsStatusAsync(
                        response: badBodyResponse,
                        expectedStatusCode: HttpStatusCode.BadRequest);

                // Act 3: Non-multipart JSON sent to POST /api/images -> 415 Unsupported Media Type
                using StringContent jsonUploadBody = new("{}", Encoding.UTF8, MediaTypeNames.Application.Json);
                using HttpResponseMessage unsupportedMediaResponse = await client.PostAsync(ApiIntegrationTestFixtures.IMAGES_ROUTE, jsonUploadBody);
                Assert.Equal(HttpStatusCode.UnsupportedMediaType, unsupportedMediaResponse.StatusCode);

                // Act 4: Payload exceeding MultipartBodyLengthLimit -> 413 Request Entity Too Large
                Dictionary<string, string?> tightMultipartOverrides = new()
                {
                        [ApiIntegrationTestFixtures.MAX_IMAGE_SIZE_CONFIG_KEY] = SMALL_MAX_IMAGE_BYTES.ToString()
                };
                using ContentApiWebApplicationFactory tightMultipartFactory = new(
                        connectionString: this._fixture.ConnectionString,
                        configurationOverrides: tightMultipartOverrides);
                using HttpClient tightClient = tightMultipartFactory.CreateClient();
                byte[] hugePayload = new byte[MULTIPART_LIMIT_EXCEEDING_BYTES];
                using HttpResponseMessage multipartLimitResponse = await ApiIntegrationTestFixtures.UploadImageAsync(
                        client: tightClient,
                        payload: hugePayload,
                        fileName: "huge.png",
                        contentType: DomainConstants.CONTENT_TYPE_PNG);
                Assert.Equal(HttpStatusCode.RequestEntityTooLarge, multipartLimitResponse.StatusCode);
        }
}
