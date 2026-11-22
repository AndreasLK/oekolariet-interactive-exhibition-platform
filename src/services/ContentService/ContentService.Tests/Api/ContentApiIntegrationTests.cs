using System.Net;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ContentService.Api.Contracts;
using ContentService.Application.Common;
using ContentService.Application.Models;
using ContentService.Domain.Common;
using ContentService.Domain.Enums;
using ContentService.Tests.Application.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace ContentService.Tests.Api;

/// <summary>
/// End-to-end HTTP integration tests verifying <c>ContentService.Api</c> routes, streaming, ETag caching, ProblemDetails, and SQL Server persistence.
/// </summary>
[Collection(MsSqlContentDatabaseCollection.COLLECTION_NAME)]
public sealed class ContentApiIntegrationTests : IAsyncLifetime
{
        /// <summary>
        /// Images API route prefix.
        /// </summary>
        private const string IMAGES_ROUTE = "/api/images";

        /// <summary>
        /// Templates API route prefix.
        /// </summary>
        private const string TEMPLATES_ROUTE = "/api/templates";

        /// <summary>
        /// Publish bundle resolution endpoint path.
        /// </summary>
        private const string PUBLISH_BUNDLES_ROUTE = "/api/publish-bundles/resolve";

        /// <summary>
        /// Storage health endpoint path.
        /// </summary>
        private const string STORAGE_HEALTH_ROUTE = "/api/health/storage";

        /// <summary>
        /// Configuration key for maximum image upload size in bytes.
        /// </summary>
        private const string MAX_IMAGE_SIZE_CONFIG_KEY = "ContentStorage:MaxImageSizeBytes";

        /// <summary>
        /// Configuration key for total node storage quota in bytes.
        /// </summary>
        private const string TOTAL_QUOTA_CONFIG_KEY = "ContentStorage:TotalQuotaBytes";

        /// <summary>
        /// Configuration key for storage health warning threshold percentage.
        /// </summary>
        private const string WARNING_THRESHOLD_CONFIG_KEY = "ContentStorage:WarningThresholdPercent";

        /// <summary>
        /// Configuration key for storage health critical threshold percentage.
        /// </summary>
        private const string CRITICAL_THRESHOLD_CONFIG_KEY = "ContentStorage:CriticalThresholdPercent";

        /// <summary>
        /// Actor identifier header name supported by <c>ContentService.Api</c>.
        /// </summary>
        private const string ACTOR_HEADER_NAME = "X-Actor-Id";

        /// <summary>
        /// Sample test actor identifier.
        /// </summary>
        private const string TEST_ACTOR_ID = "api-curator";

        /// <summary>
        /// Canonical multipart form field name for image uploads.
        /// </summary>
        private const string FILE_FORM_FIELD_NAME = "file";

        /// <summary>
        /// Optional multipart form field name for image alt text.
        /// </summary>
        private const string ALT_TEXT_FORM_FIELD_NAME = "altText";

        /// <summary>
        /// Expected <c>X-Content-Type-Options</c> value emitted on image stream responses.
        /// </summary>
        private const string EXPECTED_NOSNIFF_HEADER = "nosniff";

        /// <summary>
        /// Expected <c>Accept-Ranges</c> value emitted on sequential SQL streams.
        /// </summary>
        private const string EXPECTED_ACCEPT_RANGES_HEADER = "none";

        /// <summary>
        /// Expected inline <c>Content-Disposition</c> disposition type.
        /// </summary>
        private const string EXPECTED_INLINE_DISPOSITION = "inline";

        /// <summary>
        /// Tight max image size limit in bytes used to exercise oversized upload HTTP 400 responses.
        /// </summary>
        private const int SMALL_MAX_IMAGE_BYTES = 64;

        /// <summary>
        /// Oversized PNG payload length exceeding <see cref="SMALL_MAX_IMAGE_BYTES"/>.
        /// </summary>
        private const int OVERSIZED_PNG_BYTES = 128;

        /// <summary>
        /// Tight storage quota in bytes used to exercise HTTP 507 responses.
        /// </summary>
        private const int TIGHT_QUOTA_BYTES = 8;

        /// <summary>
        /// Payload size exceeding <c>MaxImageSizeBytes (64)</c> plus the 1 MiB multipart framing allowance to exercise HTTP 413.
        /// </summary>
        private const int MULTIPART_LIMIT_EXCEEDING_BYTES = 1_200_000;

        /// <summary>
        /// Shared JSON serializer options matching <c>ContentService.Api</c> enum string serialization.
        /// </summary>
        private static readonly JsonSerializerOptions API_JSON_OPTIONS = CreateApiJsonOptions();

        /// <summary>
        /// Minimal valid PNG payload used across image upload and streaming tests.
        /// </summary>
        private static readonly byte[] VALID_PNG_PAYLOAD = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02, 0x03, 0x04];

        /// <summary>
        /// Shared SQL Server Testcontainers fixture.
        /// </summary>
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

        /// <summary>
        /// Resets all SQL tables before each test executes.
        /// </summary>
        /// <returns>A task representing asynchronous database cleanup.</returns>
        public Task InitializeAsync() => this._fixture.ResetDatabaseAsync();

        /// <summary>
        /// Completes per-test cleanup without tearing down the shared container.
        /// </summary>
        /// <returns>A completed task.</returns>
        public Task DisposeAsync() => Task.CompletedTask;

        /// <summary>
        /// Verifies the full HTTP image lifecycle: upload (201), metadata (200), list (200), content stream + ETag (200), conditional <c>If-None-Match</c> (304), and delete (204 -> 404).
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task ImagesApi_FullLifecycle_UploadsStreamsCachesAndDeletesCorrectlyAsync()
        {
                // Arrange
                using ContentApiWebApplicationFactory factory = new(connectionString: this._fixture.ConnectionString);
                using HttpClient client = factory.CreateClient();

                // Act 1: Upload valid PNG with parameterized Content-Type header to verify media type extraction.
                using HttpResponseMessage uploadResponse = await UploadImageAsync(
                        client: client,
                        payload: VALID_PNG_PAYLOAD,
                        fileName: "exhibit-hero.png",
                        contentType: $"{DomainConstants.CONTENT_TYPE_PNG}; charset=binary",
                        altText: "Hero photo");

                // Assert 1
                Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
                ImageMetadataResult? uploaded = await uploadResponse.Content.ReadFromJsonAsync<ImageMetadataResult>(
                        options: API_JSON_OPTIONS);
                Assert.NotNull(uploaded);
                Assert.Equal($"{IMAGES_ROUTE}/{uploaded.Id}", uploadResponse.Headers.Location?.ToString());
                Assert.Equal("exhibit-hero.png", uploaded.FileName);
                Assert.Equal(DomainConstants.CONTENT_TYPE_PNG, uploaded.ContentType);
                Assert.Equal("Hero photo", uploaded.AltText);
                Assert.Equal(VALID_PNG_PAYLOAD.Length, uploaded.SizeBytes);

                // Act 2: Metadata + List
                using HttpResponseMessage metadataResponse = await client.GetAsync($"{IMAGES_ROUTE}/{uploaded.Id}");
                using HttpResponseMessage listResponse = await client.GetAsync($"{IMAGES_ROUTE}?page=1&pageSize=10&searchTerm=hero");

                // Assert 2
                Assert.Equal(HttpStatusCode.OK, metadataResponse.StatusCode);
                Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
                PagedResult<ImageMetadataResult>? listed = await listResponse.Content.ReadFromJsonAsync<PagedResult<ImageMetadataResult>>(
                        options: API_JSON_OPTIONS);
                Assert.NotNull(listed);
                Assert.Single(listed.Items);

                // Act 3: Stream content
                using HttpResponseMessage contentResponse = await client.GetAsync($"{IMAGES_ROUTE}/{uploaded.Id}/content");
                byte[] downloadedBytes = await contentResponse.Content.ReadAsByteArrayAsync();

                // Assert 3
                Assert.Equal(HttpStatusCode.OK, contentResponse.StatusCode);
                string expectedETag = $"\"{uploaded.Sha256Hash}\"";
                Assert.Equal(expectedETag, contentResponse.Headers.ETag?.ToString());
                Assert.True(contentResponse.Headers.CacheControl?.NoCache);
                Assert.True(contentResponse.Headers.CacheControl?.Private);
                Assert.Equal(EXPECTED_NOSNIFF_HEADER, contentResponse.Headers.GetValues(HeaderNames.XContentTypeOptions).Single());
                Assert.Equal(EXPECTED_ACCEPT_RANGES_HEADER, contentResponse.Headers.AcceptRanges.Single());
                Assert.Equal(EXPECTED_INLINE_DISPOSITION, contentResponse.Content.Headers.ContentDisposition?.DispositionType);
                Assert.Equal(VALID_PNG_PAYLOAD.Length, contentResponse.Content.Headers.ContentLength);
                Assert.Equal(VALID_PNG_PAYLOAD, downloadedBytes);

                // Act 4: Conditional ETag hit
                using HttpRequestMessage conditionalRequest = new(HttpMethod.Get, $"{IMAGES_ROUTE}/{uploaded.Id}/content");
                conditionalRequest.Headers.TryAddWithoutValidation(HeaderNames.IfNoneMatch, expectedETag);
                using HttpResponseMessage notModifiedResponse = await client.SendAsync(conditionalRequest);
                byte[] emptyBody = await notModifiedResponse.Content.ReadAsByteArrayAsync();

                // Assert 4
                Assert.Equal(HttpStatusCode.NotModified, notModifiedResponse.StatusCode);
                Assert.Equal(expectedETag, notModifiedResponse.Headers.ETag?.ToString());
                Assert.Empty(emptyBody);

                // Act 5: Delete unreferenced image -> 204 -> 404
                using HttpResponseMessage deleteResponse = await client.DeleteAsync($"{IMAGES_ROUTE}/{uploaded.Id}");
                using HttpResponseMessage afterDeleteMetadata = await client.GetAsync($"{IMAGES_ROUTE}/{uploaded.Id}");
                using HttpResponseMessage afterDeleteContent = await client.GetAsync($"{IMAGES_ROUTE}/{uploaded.Id}/content");

                // Assert 5
                Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, afterDeleteMetadata.StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, afterDeleteContent.StatusCode);
        }

        /// <summary>
        /// Verifies that spoofed magic bytes, oversized image uploads, and quota exhaustion map to 400 and 507 ProblemDetails responses.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task ImagesApi_WhenInvalidMagicBytesOversizedOrQuotaExceeded_ReturnsExpectedProblemDetailsAsync()
        {
                // Arrange: tight MaxImageSizeBytes = 64
                Dictionary<string, string?> sizeOverrides = new()
                {
                        [MAX_IMAGE_SIZE_CONFIG_KEY] = SMALL_MAX_IMAGE_BYTES.ToString()
                };
                using ContentApiWebApplicationFactory sizeFactory = new(
                        connectionString: this._fixture.ConnectionString,
                        configurationOverrides: sizeOverrides);
                using HttpClient sizeClient = sizeFactory.CreateClient();

                byte[] spoofedSvg = Encoding.UTF8.GetBytes("<svg><script>alert(1)</script></svg>");
                byte[] oversizedPng = new byte[OVERSIZED_PNG_BYTES];
                Array.Copy(sourceArray: VALID_PNG_PAYLOAD, destinationArray: oversizedPng, length: VALID_PNG_PAYLOAD.Length);

                // Act 1 & 2: Invalid magic bytes & oversized upload
                using HttpResponseMessage spoofedResponse = await UploadImageAsync(
                        client: sizeClient,
                        payload: spoofedSvg,
                        fileName: "spoofed.png",
                        contentType: DomainConstants.CONTENT_TYPE_PNG);
                using HttpResponseMessage oversizedResponse = await UploadImageAsync(
                        client: sizeClient,
                        payload: oversizedPng,
                        fileName: "too-big.png",
                        contentType: DomainConstants.CONTENT_TYPE_PNG);

                // Assert 1 & 2
                Assert.Equal(HttpStatusCode.BadRequest, spoofedResponse.StatusCode);
                ProblemDetails? spoofedProblem = await spoofedResponse.Content.ReadFromJsonAsync<ProblemDetails>();
                Assert.NotNull(spoofedProblem);
                Assert.Equal(StatusCodes.Status400BadRequest, spoofedProblem.Status);

                Assert.Equal(HttpStatusCode.BadRequest, oversizedResponse.StatusCode);
                ProblemDetails? oversizedProblem = await oversizedResponse.Content.ReadFromJsonAsync<ProblemDetails>();
                Assert.NotNull(oversizedProblem);
                Assert.Equal(StatusCodes.Status400BadRequest, oversizedProblem.Status);

                // Arrange: tight quota = 8 bytes
                Dictionary<string, string?> quotaOverrides = new()
                {
                        [TOTAL_QUOTA_CONFIG_KEY] = TIGHT_QUOTA_BYTES.ToString()
                };
                using ContentApiWebApplicationFactory quotaFactory = new(
                        connectionString: this._fixture.ConnectionString,
                        configurationOverrides: quotaOverrides);
                using HttpClient quotaClient = quotaFactory.CreateClient();

                // Act 3: Quota exceeded
                using HttpResponseMessage quotaResponse = await UploadImageAsync(
                        client: quotaClient,
                        payload: VALID_PNG_PAYLOAD,
                        fileName: "over-quota.png",
                        contentType: DomainConstants.CONTENT_TYPE_PNG);

                // Assert 3
                Assert.Equal(HttpStatusCode.InsufficientStorage, quotaResponse.StatusCode);
                ProblemDetails? quotaProblem = await quotaResponse.Content.ReadFromJsonAsync<ProblemDetails>();
                Assert.NotNull(quotaProblem);
                Assert.Equal(StatusCodes.Status507InsufficientStorage, quotaProblem.Status);
        }

        /// <summary>
        /// Verifies that deleting an image referenced by a template returns <c>409 Conflict</c> with <see cref="ProblemDetails"/>.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task DeleteImageApi_WhenReferencedByTemplate_Returns409ConflictProblemDetailsAsync()
        {
                // Arrange
                using ContentApiWebApplicationFactory factory = new(connectionString: this._fixture.ConnectionString);
                using HttpClient client = factory.CreateClient();

                using HttpResponseMessage uploadResponse = await UploadImageAsync(
                        client: client,
                        payload: VALID_PNG_PAYLOAD,
                        fileName: "linked.png",
                        contentType: DomainConstants.CONTENT_TYPE_PNG);
                ImageMetadataResult? image = await uploadResponse.Content.ReadFromJsonAsync<ImageMetadataResult>(API_JSON_OPTIONS);
                Assert.NotNull(image);

                CreateTemplateRequest createTemplate = new(
                        Name: "Referencing Template",
                        ComponentType: TemplateComponentType.QuizTemplate,
                        SchemaJson: $"{{\"hero\":\"{image.PictureReference}\"}}");
                using HttpResponseMessage templateResponse = await client.PostAsJsonAsync(
                        requestUri: TEMPLATES_ROUTE,
                        value: createTemplate,
                        options: API_JSON_OPTIONS);
                Assert.Equal(HttpStatusCode.Created, templateResponse.StatusCode);

                // Act
                using HttpResponseMessage deleteResponse = await client.DeleteAsync($"{IMAGES_ROUTE}/{image.Id}");

                // Assert
                Assert.Equal(HttpStatusCode.Conflict, deleteResponse.StatusCode);
                ProblemDetails? problem = await deleteResponse.Content.ReadFromJsonAsync<ProblemDetails>();
                Assert.NotNull(problem);
                Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
        }

        /// <summary>
        /// Verifies the full HTTP template CRUD lifecycle, duplicate-name 400 validation, stale concurrency token 409 Conflict, and archive delete behavior.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task TemplatesApi_CrudDuplicateConcurrencyAndArchive_ReturnsExpectedResponsesAsync()
        {
                // Arrange
                using ContentApiWebApplicationFactory factory = new(connectionString: this._fixture.ConnectionString);
                using HttpClient client = factory.CreateClient();
                client.DefaultRequestHeaders.Add(ACTOR_HEADER_NAME, TEST_ACTOR_ID);

                CreateTemplateRequest createRequest = new(
                        Name: "Water Exhibit",
                        ComponentType: TemplateComponentType.InfoScreen,
                        SchemaJson: "{}");

                // Act 1: Create -> 201
                using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
                        requestUri: TEMPLATES_ROUTE,
                        value: createRequest,
                        options: API_JSON_OPTIONS);
                Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
                TemplateResult? created = await createResponse.Content.ReadFromJsonAsync<TemplateResult>(API_JSON_OPTIONS);
                Assert.NotNull(created);
                Assert.Equal($"{TEMPLATES_ROUTE}/{created.Id}", createResponse.Headers.Location?.ToString());

                // Act 2: Duplicate name -> 400 ProblemDetails
                using HttpResponseMessage duplicateResponse = await client.PostAsJsonAsync(
                        requestUri: TEMPLATES_ROUTE,
                        value: createRequest with { Name = "  water exhibit " },
                        options: API_JSON_OPTIONS);
                Assert.Equal(HttpStatusCode.BadRequest, duplicateResponse.StatusCode);
                ProblemDetails? duplicateProblem = await duplicateResponse.Content.ReadFromJsonAsync<ProblemDetails>();
                Assert.NotNull(duplicateProblem);
                Assert.Equal(StatusCodes.Status400BadRequest, duplicateProblem.Status);

                // Act 3: Get & List -> 200, Missing -> 404
                using HttpResponseMessage getResponse = await client.GetAsync($"{TEMPLATES_ROUTE}/{created.Id}");
                using HttpResponseMessage missingResponse = await client.GetAsync($"{TEMPLATES_ROUTE}/{Guid.NewGuid()}");
                using HttpResponseMessage listResponse = await client.GetAsync($"{TEMPLATES_ROUTE}?componentType=InfoScreen&searchTerm=water");
                Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
                Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

                // Act 4: Update -> 200, Stale token -> 409 ProblemDetails
                UpdateTemplateRequest updateRequest = new(
                        Name: "Water Exhibit V2",
                        SchemaJson: "{\"version\":2}",
                        ExpectedConcurrencyStamp: created.ConcurrencyStamp);
                using HttpResponseMessage updateResponse = await client.PutAsJsonAsync(
                        requestUri: $"{TEMPLATES_ROUTE}/{created.Id}",
                        value: updateRequest,
                        options: API_JSON_OPTIONS);
                Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
                TemplateResult? updated = await updateResponse.Content.ReadFromJsonAsync<TemplateResult>(API_JSON_OPTIONS);
                Assert.NotNull(updated);
                Assert.Equal(DomainConstants.INITIAL_VERSION + 1, updated.SchemaVersion);

                using HttpResponseMessage staleUpdateResponse = await client.PutAsJsonAsync(
                        requestUri: $"{TEMPLATES_ROUTE}/{created.Id}",
                        value: updateRequest,
                        options: API_JSON_OPTIONS);
                Assert.Equal(HttpStatusCode.Conflict, staleUpdateResponse.StatusCode);
                ProblemDetails? staleProblem = await staleUpdateResponse.Content.ReadFromJsonAsync<ProblemDetails>();
                Assert.NotNull(staleProblem);
                Assert.Equal(StatusCodes.Status409Conflict, staleProblem.Status);

                // Act 5: Delete (archive) -> 204, archived GET -> 200 (IsArchived == true), second delete -> 404
                using HttpResponseMessage deleteResponse = await client.DeleteAsync($"{TEMPLATES_ROUTE}/{created.Id}");
                using HttpResponseMessage archivedGetResponse = await client.GetAsync($"{TEMPLATES_ROUTE}/{created.Id}");
                using HttpResponseMessage secondDeleteResponse = await client.DeleteAsync($"{TEMPLATES_ROUTE}/{created.Id}");
                Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
                Assert.Equal(HttpStatusCode.OK, archivedGetResponse.StatusCode);
                TemplateResult? archivedTemplate = await archivedGetResponse.Content.ReadFromJsonAsync<TemplateResult>(API_JSON_OPTIONS);
                Assert.NotNull(archivedTemplate);
                Assert.True(archivedTemplate.IsArchived);
                Assert.Equal(HttpStatusCode.NotFound, secondDeleteResponse.StatusCode);
        }

        /// <summary>
        /// Verifies publish bundle resolution (200 deterministic checksum), missing template/image (404), malformed JSON (400), and storage health (200).
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task PublishingAndHealthApis_VerifyBundleChecksumErrorMappingAndHealthMetricsAsync()
        {
                // Arrange
                Dictionary<string, string?> overrides = new()
                {
                        [TOTAL_QUOTA_CONFIG_KEY] = "100",
                        [WARNING_THRESHOLD_CONFIG_KEY] = "10.0",
                        [CRITICAL_THRESHOLD_CONFIG_KEY] = "90.0"
                };
                using ContentApiWebApplicationFactory factory = new(
                        connectionString: this._fixture.ConnectionString,
                        configurationOverrides: overrides);
                using HttpClient client = factory.CreateClient();

                // Health when empty -> Healthy, 0 bytes
                using HttpResponseMessage emptyHealthResponse = await client.GetAsync(STORAGE_HEALTH_ROUTE);
                Assert.Equal(HttpStatusCode.OK, emptyHealthResponse.StatusCode);
                StorageHealthResult? emptyHealth = await emptyHealthResponse.Content.ReadFromJsonAsync<StorageHealthResult>(API_JSON_OPTIONS);
                Assert.NotNull(emptyHealth);
                Assert.Equal(StorageHealthStatus.Healthy, emptyHealth.Status);
                Assert.Equal(0L, emptyHealth.UsedBytes);

                // Upload 12-byte PNG -> 12% utilization -> Warning
                using HttpResponseMessage uploadResponse = await UploadImageAsync(
                        client: client,
                        payload: VALID_PNG_PAYLOAD,
                        fileName: "bundle.png",
                        contentType: DomainConstants.CONTENT_TYPE_PNG);
                ImageMetadataResult? image = await uploadResponse.Content.ReadFromJsonAsync<ImageMetadataResult>(API_JSON_OPTIONS);
                Assert.NotNull(image);

                using HttpResponseMessage warningHealthResponse = await client.GetAsync(STORAGE_HEALTH_ROUTE);
                StorageHealthResult? warningHealth = await warningHealthResponse.Content.ReadFromJsonAsync<StorageHealthResult>(API_JSON_OPTIONS);
                Assert.NotNull(warningHealth);
                Assert.Equal(StorageHealthStatus.Warning, warningHealth.Status);
                Assert.Equal(VALID_PNG_PAYLOAD.Length, warningHealth.UsedBytes);

                // Publish bundle -> 200
                ResolvePublishBundleCommand validBundleCommand = new()
                {
                        TemplateId = null,
                        ImplementationJson = $"{{\"bg\":\"{image.PictureReference}\"}}"
                };
                using HttpResponseMessage bundleResponse = await client.PostAsJsonAsync(
                        requestUri: PUBLISH_BUNDLES_ROUTE,
                        value: validBundleCommand,
                        options: API_JSON_OPTIONS);
                Assert.Equal(HttpStatusCode.OK, bundleResponse.StatusCode);
                PublishBundleResult? bundle = await bundleResponse.Content.ReadFromJsonAsync<PublishBundleResult>(API_JSON_OPTIONS);
                Assert.NotNull(bundle);
                Assert.Equal(DomainConstants.SHA256_HEX_LENGTH, bundle.BundleChecksum.Length);
                BundleAssetResult asset = Assert.Single(bundle.Assets);
                Assert.Equal(image.Id, asset.ImageId);

                // Missing template -> 404 & Missing image -> 404
                using HttpResponseMessage missingTemplateResponse = await client.PostAsJsonAsync(
                        requestUri: PUBLISH_BUNDLES_ROUTE,
                        value: new ResolvePublishBundleCommand { TemplateId = Guid.NewGuid(), ImplementationJson = "{}" },
                        options: API_JSON_OPTIONS);
                Assert.Equal(HttpStatusCode.NotFound, missingTemplateResponse.StatusCode);

                using HttpResponseMessage missingImageResponse = await client.PostAsJsonAsync(
                        requestUri: PUBLISH_BUNDLES_ROUTE,
                        value: new ResolvePublishBundleCommand
                        {
                                TemplateId = null,
                                ImplementationJson = $"{{\"missing\":\"picture.node-1.{Guid.NewGuid():D}\"}}"
                        },
                        options: API_JSON_OPTIONS);
                Assert.Equal(HttpStatusCode.NotFound, missingImageResponse.StatusCode);

                // Malformed JSON -> 400 & Base64 data URI -> 400
                using HttpResponseMessage malformedJsonResponse = await client.PostAsJsonAsync(
                        requestUri: PUBLISH_BUNDLES_ROUTE,
                        value: new ResolvePublishBundleCommand { TemplateId = null, ImplementationJson = "{bad-json" },
                        options: API_JSON_OPTIONS);
                Assert.Equal(HttpStatusCode.BadRequest, malformedJsonResponse.StatusCode);

                using HttpResponseMessage base64Response = await client.PostAsJsonAsync(
                        requestUri: PUBLISH_BUNDLES_ROUTE,
                        value: new ResolvePublishBundleCommand
                        {
                                TemplateId = null,
                                ImplementationJson = "{\"img\":\"data:image/png;base64,AAAA\"}"
                        },
                        options: API_JSON_OPTIONS);
                Assert.Equal(HttpStatusCode.BadRequest, base64Response.StatusCode);
        }

        /// <summary>
        /// Verifies CORS preflight headers, CORS preservation on error responses, and transport error mappings (400, 413, 415).
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
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
                using HttpRequestMessage preflight = new(HttpMethod.Options, IMAGES_ROUTE);
                preflight.Headers.Add(HeaderNames.Origin, ALLOWED_ORIGIN);
                preflight.Headers.Add(HeaderNames.AccessControlRequestMethod, HttpMethod.Post.Method);
                using HttpResponseMessage preflightResponse = await client.SendAsync(preflight);

                // Assert 1
                Assert.Equal(HttpStatusCode.NoContent, preflightResponse.StatusCode);
                Assert.True(preflightResponse.Headers.TryGetValues(HeaderNames.AccessControlAllowOrigin, out IEnumerable<string>? origins));
                Assert.Contains(ALLOWED_ORIGIN, origins);

                // Act 2: Cross-origin malformed JSON payload on POST /api/templates -> 400 ProblemDetails WITH CORS headers preserved
                using HttpRequestMessage badCrossOriginRequest = new(HttpMethod.Post, TEMPLATES_ROUTE)
                {
                        Content = new StringContent("{not-valid-json", Encoding.UTF8, MediaTypeNames.Application.Json)
                };
                badCrossOriginRequest.Headers.Add(HeaderNames.Origin, ALLOWED_ORIGIN);
                using HttpResponseMessage badBodyResponse = await client.SendAsync(badCrossOriginRequest);

                // Assert 2
                Assert.Equal(HttpStatusCode.BadRequest, badBodyResponse.StatusCode);
                Assert.True(badBodyResponse.Headers.TryGetValues(HeaderNames.AccessControlAllowOrigin, out IEnumerable<string>? errorOrigins));
                Assert.Contains(ALLOWED_ORIGIN, errorOrigins);
                ProblemDetails? problem = await badBodyResponse.Content.ReadFromJsonAsync<ProblemDetails>();
                Assert.NotNull(problem);
                Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);

                // Act 3: Non-multipart JSON sent to POST /api/images -> 415 Unsupported Media Type (preserved, not clobbered to 400)
                using StringContent jsonUploadBody = new("{}", Encoding.UTF8, MediaTypeNames.Application.Json);
                using HttpResponseMessage unsupportedMediaResponse = await client.PostAsync(IMAGES_ROUTE, jsonUploadBody);
                Assert.Equal(HttpStatusCode.UnsupportedMediaType, unsupportedMediaResponse.StatusCode);

                // Act 4: Payload exceeding MultipartBodyLengthLimit -> 413 Request Entity Too Large
                Dictionary<string, string?> tightMultipartOverrides = new()
                {
                        [MAX_IMAGE_SIZE_CONFIG_KEY] = SMALL_MAX_IMAGE_BYTES.ToString()
                };
                using ContentApiWebApplicationFactory tightMultipartFactory = new(
                        connectionString: this._fixture.ConnectionString,
                        configurationOverrides: tightMultipartOverrides);
                using HttpClient tightClient = tightMultipartFactory.CreateClient();
                byte[] hugePayload = new byte[MULTIPART_LIMIT_EXCEEDING_BYTES];
                using HttpResponseMessage multipartLimitResponse = await UploadImageAsync(
                        client: tightClient,
                        payload: hugePayload,
                        fileName: "huge.png",
                        contentType: DomainConstants.CONTENT_TYPE_PNG);
                Assert.Equal(HttpStatusCode.RequestEntityTooLarge, multipartLimitResponse.StatusCode);
        }

        /// <summary>
        /// Sends a multipart image upload request to <c>POST /api/images</c>.
        /// </summary>
        /// <param name="client">The test HTTP client.</param>
        /// <param name="payload">The binary payload bytes.</param>
        /// <param name="fileName">The uploaded file name.</param>
        /// <param name="contentType">The multipart part <c>Content-Type</c> value.</param>
        /// <param name="altText">Optional image alt text form value.</param>
        /// <returns>The HTTP response returned by <c>POST /api/images</c>.</returns>
        private static async Task<HttpResponseMessage> UploadImageAsync(
                HttpClient client,
                byte[] payload,
                string fileName,
                string contentType,
                string? altText = null)
        {
                using MultipartFormDataContent form = new();
                using ByteArrayContent fileContent = new(payload);
                fileContent.Headers.TryAddWithoutValidation(HeaderNames.ContentType, contentType);
                form.Add(fileContent, FILE_FORM_FIELD_NAME, fileName);

                if (altText is not null)
                {
                        form.Add(new StringContent(altText), ALT_TEXT_FORM_FIELD_NAME);
                }

                using HttpRequestMessage request = new(HttpMethod.Post, IMAGES_ROUTE)
                {
                        Content = form
                };
                request.Headers.Add(ACTOR_HEADER_NAME, TEST_ACTOR_ID);
                return await client.SendAsync(request);
        }

        /// <summary>
        /// Creates JSON serializer options configured with <see cref="JsonStringEnumConverter"/> matching <c>ContentService.Api</c>.
        /// </summary>
        /// <returns>A configured <see cref="JsonSerializerOptions"/> instance.</returns>
        private static JsonSerializerOptions CreateApiJsonOptions()
        {
                JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
                options.Converters.Add(new JsonStringEnumConverter());
                return options;
        }
}
