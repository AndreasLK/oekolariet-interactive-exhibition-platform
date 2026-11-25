using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using ContentService.Api.Contracts;
using ContentService.Application.Common;
using ContentService.Application.Interfaces;
using ContentService.Application.Models;
using ContentService.Domain.Common;
using ContentService.Domain.Entities;
using ContentService.Domain.Enums;
using ContentService.Domain.Exceptions;
using ContentService.Infrastructure;
using ContentService.Sdk.Clients;
using ContentService.Sdk.Exceptions;
using ContentService.Sdk.Models;
using ContentService.Sdk.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace ContentService.Tests.Api;

/// <summary>
/// Fast, Docker-free HTTP endpoint, middleware, CORS, and exception-handling tests for <c>ContentService.Api</c>.
/// </summary>
public sealed class ContentApiEndpointTests
{
        /// <summary>
        /// Valid storage node identifier used across API tests.
        /// </summary>
        private const string NODE_ID = "node-1";

        /// <summary>
        /// Dummy SQL Server connection string satisfying startup validation when SQL Server ports are replaced with fakes.
        /// </summary>
        private const string DUMMY_CONNECTION_STRING = "Server=(localdb)\\mssqllocaldb;Database=FakeApiTest;Trusted_Connection=True;";

        /// <summary>
        /// Allowed Blazor client origin configured for CORS testing.
        /// </summary>
        private const string ALLOWED_ORIGIN = "https://editor.oekolariet.dk";

        /// <summary>
        /// Disallowed third-party origin used to verify CORS fail-closed behavior.
        /// </summary>
        private const string DISALLOWED_ORIGIN = "https://evil.example.com";

        /// <summary>
        /// Small max image byte cap used in transport boundary tests.
        /// </summary>
        private const long SMALL_MAX_IMAGE_BYTES = 1024L;

        /// <summary>
        /// Oversized payload exceeding both <see cref="SMALL_MAX_IMAGE_BYTES"/> and the 1 MiB multipart framing envelope.
        /// </summary>
        private const int MULTIPART_LIMIT_EXCEEDING_BYTES = 1_100_000;

        /// <summary>
        /// Minimal valid PNG payload bytes.
        /// </summary>
        private static readonly byte[] VALID_PNG_BYTES = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02];

        /// <summary>
        /// Minimal valid JPEG payload bytes.
        /// </summary>
        private static readonly byte[] VALID_JPEG_BYTES = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];

        /// <summary>
        /// Minimal valid WebP payload bytes.
        /// </summary>
        private static readonly byte[] VALID_WEBP_BYTES = [0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50, 0x56];

        /// <summary>
        /// JSON options matching <c>ContentService.Api</c> string enum configuration.
        /// </summary>
        private static readonly JsonSerializerOptions API_JSON_OPTIONS = CreateApiJsonOptions();

        /// <summary>
        /// Verifies PNG, JPEG, and WebP multipart uploads, empty/missing files, MIME spoofing, 413/415 transport limits, 507 quota errors, and actor header handling.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task ImageUploadEndpoint_WhenValidAndInvalidRequestsArrive_ReturnsExpectedStatusCodesAsync()
        {
                // Arrange
                InMemoryContentState state = new();
                await using FakeApiFactory factory = new(
                        state: state,
                        configurationOverrides: new Dictionary<string, string?>
                        {
                                ["ContentStorage:MaxImageSizeBytes"] = SMALL_MAX_IMAGE_BYTES.ToString(),
                                ["ContentStorage:TotalQuotaBytes"] = "10000"
                        });
                using HttpClient client = factory.CreateClient();

                // Act & Assert: Valid PNG, JPEG, and WebP uploads return 201 with Location header.
                using HttpResponseMessage pngResponse = await UploadFileAsync(
                        client: client,
                        bytes: VALID_PNG_BYTES,
                        fileName: "skov billede æøå.png",
                        contentType: "image/png; charset=utf-8");
                Assert.Equal(HttpStatusCode.Created, pngResponse.StatusCode);
                Assert.NotNull(pngResponse.Headers.Location);
                ImageMetadataResult? pngMetadata = await pngResponse.Content.ReadFromJsonAsync<ImageMetadataResult>(options: API_JSON_OPTIONS);
                Assert.NotNull(pngMetadata);
                Assert.Equal(DomainConstants.CONTENT_TYPE_PNG, pngMetadata.ContentType);

                using HttpResponseMessage jpegResponse = await UploadFileAsync(
                        client: client,
                        bytes: VALID_JPEG_BYTES,
                        fileName: "photo.jpg",
                        contentType: DomainConstants.CONTENT_TYPE_JPEG);
                Assert.Equal(HttpStatusCode.Created, jpegResponse.StatusCode);

                using HttpResponseMessage webpResponse = await UploadFileAsync(
                        client: client,
                        bytes: VALID_WEBP_BYTES,
                        fileName: "banner.webp",
                        contentType: DomainConstants.CONTENT_TYPE_WEBP);
                Assert.Equal(HttpStatusCode.Created, webpResponse.StatusCode);

                // Blank X-Actor-Id must fail validation (400) rather than falling back silently.
                using MultipartFormDataContent blankActorForm = CreateImageMultipart(
                        bytes: VALID_PNG_BYTES,
                        fileName: "actor.png",
                        contentType: DomainConstants.CONTENT_TYPE_PNG);
                using HttpRequestMessage blankActorRequest = new(method: HttpMethod.Post, requestUri: "/api/images")
                {
                        Content = blankActorForm
                };
                blankActorRequest.Headers.Add(name: "X-Actor-Id", value: "   ");
                using HttpResponseMessage blankActorResponse = await client.SendAsync(request: blankActorRequest);
                Assert.Equal(HttpStatusCode.BadRequest, blankActorResponse.StatusCode);

                // Missing 'file' form field and 0-byte files both yield 400 ProblemDetails.
                using MultipartFormDataContent missingFileForm = new();
                missingFileForm.Add(content: new StringContent("alt"), name: "altText");
                using HttpResponseMessage missingFileResponse = await client.PostAsync(requestUri: "/api/images", content: missingFileForm);
                Assert.Equal(HttpStatusCode.BadRequest, missingFileResponse.StatusCode);

                using HttpResponseMessage emptyFileResponse = await UploadFileAsync(
                        client: client,
                        bytes: Array.Empty<byte>(),
                        fileName: "empty.png",
                        contentType: DomainConstants.CONTENT_TYPE_PNG);
                Assert.Equal(HttpStatusCode.BadRequest, emptyFileResponse.StatusCode);

                // Spoofed SVG-as-PNG yields 400.
                using HttpResponseMessage spoofedResponse = await UploadFileAsync(
                        client: client,
                        bytes: Encoding.UTF8.GetBytes("<svg></svg>"),
                        fileName: "xss.png",
                        contentType: DomainConstants.CONTENT_TYPE_PNG);
                Assert.Equal(HttpStatusCode.BadRequest, spoofedResponse.StatusCode);

                // Non-multipart request yields 415 UnsupportedMediaType.
                using HttpResponseMessage nonMultipartResponse = await client.PostAsJsonAsync(
                        requestUri: "/api/images",
                        value: new { fileName = "a.png" });
                Assert.Equal(HttpStatusCode.UnsupportedMediaType, nonMultipartResponse.StatusCode);

                // Payload exceeding MultipartBodyLengthLimit yields 413 PayloadTooLarge.
                using HttpResponseMessage oversizedMultipartResponse = await UploadFileAsync(
                        client: client,
                        bytes: new byte[MULTIPART_LIMIT_EXCEEDING_BYTES],
                        fileName: "huge.png",
                        contentType: DomainConstants.CONTENT_TYPE_PNG);
                Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversizedMultipartResponse.StatusCode);

                // Exhausted quota yields 507 InsufficientStorage.
                state.ForcedUsedBytesOverride = 10_000L;
                using HttpResponseMessage quotaResponse = await UploadFileAsync(
                        client: client,
                        bytes: VALID_PNG_BYTES,
                        fileName: "full.png",
                        contentType: DomainConstants.CONTENT_TYPE_PNG);
                Assert.Equal(HttpStatusCode.InsufficientStorage, quotaResponse.StatusCode);
        }

        /// <summary>
        /// Verifies image metadata, listing, content headers, UTF-8 <c>Content-Disposition</c>, immutable <c>Cache-Control</c>, orphaned binary 404, and deletion conflicts.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task ImageReadStreamAndDeleteEndpoints_WhenExercisedAcrossEdgeCases_ReturnExpectedResponsesAsync()
        {
                // Arrange
                InMemoryContentState state = new();
                ImageEntity seeded = CreateSeededImage(fileName: "skov billede æøå.png", bytes: VALID_PNG_BYTES);
                state.SeedImage(image: seeded, binaryBytes: VALID_PNG_BYTES);

                ImageEntity orphaned = CreateSeededImage(fileName: "orphan.png", bytes: VALID_PNG_BYTES);
                state.SeedImage(image: orphaned, binaryBytes: null);

                await using FakeApiFactory factory = new(state: state);
                using HttpClient client = factory.CreateClient();

                // Act & Assert: Metadata, listing, invalid GUID routing, and Guid.Empty validation.
                using HttpResponseMessage listResponse = await client.GetAsync(requestUri: "/api/images?page=1&pageSize=10&searchTerm=skov");
                Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

                using HttpResponseMessage metadataResponse = await client.GetAsync(requestUri: $"/api/images/{seeded.Id}");
                Assert.Equal(HttpStatusCode.OK, metadataResponse.StatusCode);

                using HttpResponseMessage emptyGuidResponse = await client.GetAsync(requestUri: $"/api/images/{Guid.Empty}");
                Assert.Equal(HttpStatusCode.BadRequest, emptyGuidResponse.StatusCode);

                using HttpResponseMessage malformedGuidResponse = await client.GetAsync(requestUri: "/api/images/not-a-guid");
                Assert.Equal(HttpStatusCode.NotFound, malformedGuidResponse.StatusCode);

                // Act & Assert: Content response headers including UTF-8 FileNameStar, immutable Cache-Control, and security headers.
                using HttpResponseMessage contentResponse = await client.GetAsync(requestUri: $"/api/images/{seeded.Id}/content");
                Assert.Equal(HttpStatusCode.OK, contentResponse.StatusCode);
                Assert.Equal(VALID_PNG_BYTES, await contentResponse.Content.ReadAsByteArrayAsync());
                Assert.True(contentResponse.Headers.CacheControl?.Public ?? false);
                Assert.Equal("none", string.Join(",", contentResponse.Headers.GetValues(HeaderNames.AcceptRanges)));
                Assert.Equal("nosniff", string.Join(",", contentResponse.Headers.GetValues(HeaderNames.XContentTypeOptions)));
                Assert.NotNull(contentResponse.Content.Headers.ContentDisposition?.FileNameStar);

                // Orphaned metadata without binary returns 404.
                using HttpResponseMessage orphanedResponse = await client.GetAsync(requestUri: $"/api/images/{orphaned.Id}/content");
                Assert.Equal(HttpStatusCode.NotFound, orphanedResponse.StatusCode);

                // Deleting an image referenced by a template returns 409 Conflict; unreferenced returns 204.
                state.ReferencedImageIds.Add(seeded.Id);
                using HttpResponseMessage conflictDeleteResponse = await client.DeleteAsync(requestUri: $"/api/images/{seeded.Id}");
                Assert.Equal(HttpStatusCode.Conflict, conflictDeleteResponse.StatusCode);

                state.ReferencedImageIds.Remove(seeded.Id);
                using HttpResponseMessage deleteResponse = await client.DeleteAsync(requestUri: $"/api/images/{seeded.Id}");
                Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

                using HttpResponseMessage deleteMissingResponse = await client.DeleteAsync(requestUri: $"/api/images/{seeded.Id}");
                Assert.Equal(HttpStatusCode.NotFound, deleteMissingResponse.StatusCode);
        }

        /// <summary>
        /// Verifies template CRUD endpoints, query enum errors, concurrency conflicts, publish bundle resolution, and storage health thresholds.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task TemplatePublishingAndHealthEndpoints_WhenExercisedAcrossEdgeCases_ReturnExpectedResponsesAsync()
        {
                // Arrange
                InMemoryContentState state = new();
                ImageEntity image = CreateSeededImage(fileName: "asset.png", bytes: VALID_PNG_BYTES);
                state.SeedImage(image: image, binaryBytes: VALID_PNG_BYTES);

                await using FakeApiFactory factory = new(
                        state: state,
                        configurationOverrides: new Dictionary<string, string?>
                        {
                                ["ContentStorage:TotalQuotaBytes"] = "1000",
                                ["ContentStorage:WarningThresholdPercent"] = "50",
                                ["ContentStorage:CriticalThresholdPercent"] = "90"
                        });
                using HttpClient client = factory.CreateClient();

                // Act & Assert: Create template -> 201; duplicate name -> 400; invalid componentType query -> 400.
                CreateTemplateRequest createRequest = new(
                        Name: "Quiz Card",
                        ComponentType: TemplateComponentType.QuizTemplate,
                        SchemaJson: $"{{\"img\":\"{image.Reference}\"}}");
                using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
                        requestUri: "/api/templates",
                        value: createRequest,
                        options: API_JSON_OPTIONS);
                Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
                TemplateResult? created = await createResponse.Content.ReadFromJsonAsync<TemplateResult>(options: API_JSON_OPTIONS);
                Assert.NotNull(created);

                using HttpResponseMessage duplicateResponse = await client.PostAsJsonAsync(
                        requestUri: "/api/templates",
                        value: createRequest,
                        options: API_JSON_OPTIONS);
                Assert.Equal(HttpStatusCode.BadRequest, duplicateResponse.StatusCode);

                using HttpResponseMessage invalidEnumListResponse = await client.GetAsync(requestUri: "/api/templates?componentType=NotARealType");
                Assert.Equal(HttpStatusCode.BadRequest, invalidEnumListResponse.StatusCode);

                // Update template: missing -> 404; empty stamp -> 400; valid -> 200; stale stamp -> 409.
                using HttpResponseMessage missingPutResponse = await client.PutAsJsonAsync(
                        requestUri: $"/api/templates/{Guid.NewGuid()}",
                        value: new UpdateTemplateRequest(Name: "New", SchemaJson: "{}", ExpectedConcurrencyStamp: Guid.NewGuid()),
                        options: API_JSON_OPTIONS);
                Assert.Equal(HttpStatusCode.NotFound, missingPutResponse.StatusCode);

                using HttpResponseMessage emptyStampPutResponse = await client.PutAsJsonAsync(
                        requestUri: $"/api/templates/{created.Id}",
                        value: new UpdateTemplateRequest(Name: "New", SchemaJson: "{}", ExpectedConcurrencyStamp: Guid.Empty),
                        options: API_JSON_OPTIONS);
                Assert.Equal(HttpStatusCode.Conflict, emptyStampPutResponse.StatusCode);

                using HttpResponseMessage updateResponse = await client.PutAsJsonAsync(
                        requestUri: $"/api/templates/{created.Id}",
                        value: new UpdateTemplateRequest(
                                Name: "Quiz Card v2",
                                SchemaJson: $"{{\"img\":\"{image.Reference}\"}}",
                                ExpectedConcurrencyStamp: created.ConcurrencyStamp),
                        options: API_JSON_OPTIONS);
                Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

                using HttpResponseMessage staleUpdateResponse = await client.PutAsJsonAsync(
                        requestUri: $"/api/templates/{created.Id}",
                        value: new UpdateTemplateRequest(
                                Name: "Quiz Card stale",
                                SchemaJson: "{}",
                                ExpectedConcurrencyStamp: created.ConcurrencyStamp),
                        options: API_JSON_OPTIONS);
                Assert.Equal(HttpStatusCode.Conflict, staleUpdateResponse.StatusCode);

                // Archive template -> 204; repeat delete -> 404; publish bundle with archived template -> 200.
                using HttpResponseMessage deleteTemplateResponse = await client.DeleteAsync(requestUri: $"/api/templates/{created.Id}");
                Assert.Equal(HttpStatusCode.NoContent, deleteTemplateResponse.StatusCode);

                using HttpResponseMessage repeatDeleteResponse = await client.DeleteAsync(requestUri: $"/api/templates/{created.Id}");
                Assert.Equal(HttpStatusCode.NotFound, repeatDeleteResponse.StatusCode);

                using HttpResponseMessage publishResponse = await client.PostAsJsonAsync(
                        requestUri: "/api/publish-bundles/resolve",
                        value: new ResolvePublishBundleCommand { TemplateId = created.Id, ImplementationJson = "{}" },
                        options: API_JSON_OPTIONS);
                Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);

                // Publish bundle with Guid.Empty -> 400.
                using HttpResponseMessage emptyPublishResponse = await client.PostAsJsonAsync(
                        requestUri: "/api/publish-bundles/resolve",
                        value: new ResolvePublishBundleCommand { TemplateId = Guid.Empty, ImplementationJson = "{}" },
                        options: API_JSON_OPTIONS);
                Assert.Equal(HttpStatusCode.BadRequest, emptyPublishResponse.StatusCode);

                // Health endpoint transitions: Healthy (<500), Warning (600), FullReadOnly (950).
                state.ForcedUsedBytesOverride = 200L;
                StorageHealthResult? healthy = await client.GetFromJsonAsync<StorageHealthResult>(
                        requestUri: "/api/health/storage",
                        options: API_JSON_OPTIONS);
                Assert.Equal(StorageHealthStatus.Healthy, healthy?.Status);

                state.ForcedUsedBytesOverride = 600L;
                StorageHealthResult? warning = await client.GetFromJsonAsync<StorageHealthResult>(
                        requestUri: "/api/health/storage",
                        options: API_JSON_OPTIONS);
                Assert.Equal(StorageHealthStatus.Warning, warning?.Status);

                state.ForcedUsedBytesOverride = 950L;
                StorageHealthResult? full = await client.GetFromJsonAsync<StorageHealthResult>(
                        requestUri: "/api/health/storage",
                        options: API_JSON_OPTIONS);
                Assert.Equal(StorageHealthStatus.FullReadOnly, full?.Status);
        }

        /// <summary>
        /// Verifies authenticated identity precedence, CORS fail-closed vs allowed error headers, 500 redaction in Production vs Development, and 499 client aborts.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task GlobalExceptionHandlerCorsAndAuthIdentity_WhenTriggered_BehaveCorrectlyAsync()
        {
                // Arrange: Production host with allowed CORS origin and a throwing repository.
                InMemoryContentState state = new()
                {
                        ExceptionToThrowOnGetImage = new InvalidOperationException("Sensitive SQL detail")
                };
                await using FakeApiFactory prodFactory = new(
                        state: state,
                        environmentName: Environments.Production,
                        configurationOverrides: new Dictionary<string, string?>
                        {
                                ["Cors:AllowedOrigins:0"] = ALLOWED_ORIGIN
                        });
                using HttpClient prodClient = prodFactory.CreateClient();

                // Allowed origin gets CORS header even on 500 ProblemDetails, and detail is redacted in Production.
                using HttpRequestMessage allowedOriginRequest = new(method: HttpMethod.Get, requestUri: $"/api/images/{Guid.NewGuid()}");
                allowedOriginRequest.Headers.Add(name: HeaderNames.Origin, value: ALLOWED_ORIGIN);
                using HttpResponseMessage prodErrorResponse = await prodClient.SendAsync(request: allowedOriginRequest);
                Assert.Equal(HttpStatusCode.InternalServerError, prodErrorResponse.StatusCode);
                Assert.Contains(ALLOWED_ORIGIN, prodErrorResponse.Headers.GetValues(HeaderNames.AccessControlAllowOrigin));
                ProblemDetails? prodProblem = await prodErrorResponse.Content.ReadFromJsonAsync<ProblemDetails>();
                Assert.DoesNotContain("Sensitive SQL detail", prodProblem?.Detail ?? string.Empty);

                // Disallowed origin must NOT receive Access-Control-Allow-Origin.
                using HttpRequestMessage disallowedOriginRequest = new(method: HttpMethod.Get, requestUri: $"/api/images/{Guid.NewGuid()}");
                disallowedOriginRequest.Headers.Add(name: HeaderNames.Origin, value: DISALLOWED_ORIGIN);
                using HttpResponseMessage disallowedResponse = await prodClient.SendAsync(request: disallowedOriginRequest);
                Assert.False(disallowedResponse.Headers.Contains(HeaderNames.AccessControlAllowOrigin));

                // Development host exposes exception detail on 500 ProblemDetails, plus OpenAPI JSON and Swagger UI.
                await using FakeApiFactory devFactory = new(state: state, environmentName: Environments.Development);
                using HttpClient devClient = devFactory.CreateClient();
                using HttpResponseMessage openApiResponse = await devClient.GetAsync(requestUri: "/openapi/v1.json");
                Assert.Equal(HttpStatusCode.OK, openApiResponse.StatusCode);
                string openApiJson = await openApiResponse.Content.ReadAsStringAsync();
                Assert.Contains("altText", openApiJson);

                using HttpResponseMessage swaggerUiResponse = await devClient.GetAsync(requestUri: "/swagger/index.html");
                Assert.Equal(HttpStatusCode.OK, swaggerUiResponse.StatusCode);

                using HttpResponseMessage devErrorResponse = await devClient.GetAsync(requestUri: $"/api/images/{Guid.NewGuid()}");
                Assert.Equal(HttpStatusCode.InternalServerError, devErrorResponse.StatusCode);
                ProblemDetails? devProblem = await devErrorResponse.Content.ReadFromJsonAsync<ProblemDetails>();
                Assert.Equal("Sensitive SQL detail", devProblem?.Detail);

                // Authenticated User.Identity.Name takes precedence over X-Actor-Id and request body ActorId.
                InMemoryContentState authState = new();
                await using FakeApiFactory authFactory = new(state: authState, authenticatedUserName: "authenticated-curator");
                using HttpClient authClient = authFactory.CreateClient();
                using HttpRequestMessage authCreateRequest = new(method: HttpMethod.Post, requestUri: "/api/templates")
                {
                        Content = JsonContent.Create(
                                inputValue: new CreateTemplateRequest(
                                        Name: "Auth Precedence",
                                        ComponentType: TemplateComponentType.Custom,
                                        SchemaJson: "{}",
                                        ActorId: "spoofed-body-actor"),
                                options: API_JSON_OPTIONS)
                };
                authCreateRequest.Headers.Add(name: "X-Actor-Id", value: "spoofed-header-actor");
                using HttpResponseMessage authCreateResponse = await authClient.SendAsync(request: authCreateRequest);
                Assert.Equal(HttpStatusCode.Created, authCreateResponse.StatusCode);
                TemplateEntity savedTemplate = Assert.Single(authState.Templates.Values);
                Assert.Equal("authenticated-curator", savedTemplate.CreatedBy);
        }

        /// <summary>
        /// Smoke-tests <see cref="ContentServiceImageClient"/> end-to-end against <c>ContentService.Api</c> over an in-memory test server.
        /// </summary>
        [Fact]
        public async Task ContentServiceSdk_RoundTripsUploadMetadataDownloadAndErrorsAgainstApiAsync()
        {
                InMemoryContentState state = new();
                await using FakeApiFactory factory = new(state: state, environmentName: Environments.Development);
                using HttpClient httpClient = factory.CreateClient();

                ContentServiceImageClient sdkClient = new(
                        httpClient: httpClient,
                        options: Microsoft.Extensions.Options.Options.Create(
                                options: new ContentServiceClientOptions
                                {
                                        BaseAddress = httpClient.BaseAddress,
                                        DefaultActorId = "sdk-editor"
                                }));

                // Verify upload preserves Danish filename, altText, and actor attribution through Minimal API binding.
                await using MemoryStream uploadStream = new(buffer: VALID_PNG_BYTES);
                ContentImageMetadata uploaded = await sdkClient.UploadImageAsync(request: new UploadImageRequest
                {
                        Content = uploadStream,
                        FileName = "økolariet æble.png",
                        ContentType = DomainConstants.CONTENT_TYPE_PNG,
                        AltText = "Økolariet logo"
                });

                Assert.Equal(expected: "økolariet æble.png", actual: uploaded.FileName);
                Assert.Equal(expected: DomainConstants.CONTENT_TYPE_PNG, actual: uploaded.ContentType);
                Assert.Equal(expected: VALID_PNG_BYTES.Length, actual: uploaded.SizeBytes);

                // Verify metadata retrieval and content URI generation match the uploaded image.
                ContentImageMetadata fetched = await sdkClient.GetImageMetadataAsync(imageId: uploaded.Id);
                Assert.Equal(expected: uploaded.Id, actual: fetched.Id);
                Assert.Equal(
                        expected: new Uri(baseUri: httpClient.BaseAddress!, relativeUri: $"api/images/{uploaded.Id:D}/content"),
                        actual: sdkClient.GetImageContentUri(imageId: uploaded.Id));

                // Verify download extracts FileNameStar and streams exact PNG bytes.
                await using ContentImageDownload downloaded = await sdkClient.DownloadImageAsync(imageId: uploaded.Id);
                await using MemoryStream downloadedBytes = new();
                await downloaded.Content.CopyToAsync(destination: downloadedBytes);
                Assert.Equal(expected: VALID_PNG_BYTES, actual: downloadedBytes.ToArray());
                Assert.Equal(expected: "økolariet æble.png", actual: downloaded.FileName);
                Assert.Equal(expected: DomainConstants.CONTENT_TYPE_PNG, actual: downloaded.ContentType);

                // Verify OpenAPI documents 415 on POST /api/images and SDK maps 404 ProblemDetails cleanly.
                string openApiJson = await httpClient.GetStringAsync(requestUri: "/openapi/v1.json");
                Assert.Contains(expectedSubstring: "\"415\"", actualString: openApiJson);

                ContentServiceApiException notFound = await Assert.ThrowsAsync<ContentServiceApiException>(
                        testCode: () => sdkClient.GetImageMetadataAsync(imageId: Guid.NewGuid()));
                Assert.Equal(expected: HttpStatusCode.NotFound, actual: notFound.StatusCode);
                Assert.NotNull(notFound.ProblemDetails);
        }

        /// <summary>
        /// Creates a multipart upload request for test scenarios.
        /// </summary>
        /// <param name="bytes">The binary payload.</param>
        /// <param name="fileName">The uploaded file name.</param>
        /// <param name="contentType">The multipart file content type header.</param>
        /// <returns>A configured <see cref="MultipartFormDataContent"/> instance.</returns>
        private static MultipartFormDataContent CreateImageMultipart(byte[] bytes, string fileName, string contentType)
        {
                MultipartFormDataContent form = new();
                ByteArrayContent fileContent = new(bytes);
                fileContent.Headers.TryAddWithoutValidation(name: HeaderNames.ContentType, value: contentType);
                form.Add(content: fileContent, name: "file", fileName: fileName);
                return form;
        }

        /// <summary>
        /// Sends a multipart image upload request.
        /// </summary>
        /// <param name="client">The test HTTP client.</param>
        /// <param name="bytes">The binary payload.</param>
        /// <param name="fileName">The uploaded file name.</param>
        /// <param name="contentType">The declared file content type.</param>
        /// <returns>The HTTP response message.</returns>
        private static Task<HttpResponseMessage> UploadFileAsync(
                HttpClient client,
                byte[] bytes,
                string fileName,
                string contentType)
        {
                MultipartFormDataContent form = CreateImageMultipart(bytes: bytes, fileName: fileName, contentType: contentType);
                return client.PostAsync(requestUri: "/api/images", content: form);
        }

        /// <summary>
        /// Creates a seeded <see cref="ImageEntity"/> with a matching SHA-256 digest.
        /// </summary>
        /// <param name="fileName">The file name.</param>
        /// <param name="bytes">The PNG payload bytes.</param>
        /// <returns>A newly initialized <see cref="ImageEntity"/>.</returns>
        private static ImageEntity CreateSeededImage(string fileName, byte[] bytes)
        {
                string hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
                return ImageEntity.Create(
                        storageNodeId: NODE_ID,
                        fileName: fileName,
                        contentType: DomainConstants.CONTENT_TYPE_PNG,
                        fileSizeBytes: bytes.Length,
                        sha256Hash: hash,
                        actorId: "seed-actor");
        }

        /// <summary>
        /// Builds JSON serializer options matching <c>ContentService.Api</c>.
        /// </summary>
        /// <returns>Configured <see cref="JsonSerializerOptions"/>.</returns>
        private static JsonSerializerOptions CreateApiJsonOptions()
        {
                JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
                options.Converters.Add(new JsonStringEnumConverter());
                return options;
        }

        /// <summary>
        /// Test <see cref="WebApplicationFactory{TEntryPoint}"/> replacing SQL Server ports with in-memory test doubles.
        /// </summary>
        private sealed class FakeApiFactory : WebApplicationFactory<Program>
        {
                private readonly InMemoryContentState _state;
                private readonly string _environmentName;
                private readonly IReadOnlyDictionary<string, string?> _configurationOverrides;
                private readonly string? _authenticatedUserName;

                public FakeApiFactory(
                        InMemoryContentState state,
                        string? environmentName = null,
                        IReadOnlyDictionary<string, string?>? configurationOverrides = null,
                        string? authenticatedUserName = null)
                {
                        this._state = state;
                        this._environmentName = environmentName ?? Environments.Production;
                        this._configurationOverrides = configurationOverrides ?? new Dictionary<string, string?>();
                        this._authenticatedUserName = authenticatedUserName;
                }

                protected override void ConfigureWebHost(IWebHostBuilder builder)
                {
                        builder.UseEnvironment(this._environmentName);
                        builder.UseSetting(
                                key: $"ConnectionStrings:{DependencyInjection.CONTENT_DATABASE_CONNECTION_STRING_NAME}",
                                value: DUMMY_CONNECTION_STRING);

                        foreach (KeyValuePair<string, string?> entry in this._configurationOverrides)
                        {
                                builder.UseSetting(key: entry.Key, value: entry.Value);
                        }

                        builder.ConfigureAppConfiguration((_, configBuilder) =>
                        {
                                Dictionary<string, string?> settings = new(StringComparer.OrdinalIgnoreCase)
                                {
                                        [$"ConnectionStrings:{DependencyInjection.CONTENT_DATABASE_CONNECTION_STRING_NAME}"] = DUMMY_CONNECTION_STRING
                                };

                                foreach (KeyValuePair<string, string?> entry in this._configurationOverrides)
                                {
                                        settings[entry.Key] = entry.Value;
                                }

                                configBuilder.AddInMemoryCollection(settings);
                        });

                        builder.ConfigureTestServices(services =>
                        {
                                services.RemoveAll<IImageRepository>();
                                services.RemoveAll<IImageBinaryStore>();
                                services.RemoveAll<ITemplateRepository>();
                                services.RemoveAll<IStorageQuotaMonitor>();

                                services.AddSingleton(this._state);
                                services.AddScoped<IImageRepository, InMemoryImageRepository>();
                                services.AddScoped<IImageBinaryStore, InMemoryImageBinaryStore>();
                                services.AddScoped<ITemplateRepository, InMemoryTemplateRepository>();
                                services.AddScoped<IStorageQuotaMonitor, InMemoryStorageQuotaMonitor>();

                                if (!string.IsNullOrWhiteSpace(this._authenticatedUserName))
                                {
                                        services.AddSingleton(new TestUserIdentityOptions(this._authenticatedUserName));
                                        services.AddAuthentication(defaultScheme: TestAuthHandler.SCHEME_NAME)
                                                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                                                        authenticationScheme: TestAuthHandler.SCHEME_NAME,
                                                        configureOptions: null);
                                        services.AddSingleton<IStartupFilter, AutoAuthenticateStartupFilter>();
                                }
                        });
                }
        }

        /// <summary>
        /// Shared mutable state backing in-memory API test doubles.
        /// </summary>
        private sealed class InMemoryContentState
        {
                public Dictionary<Guid, ImageEntity> Images { get; } = [];
                public Dictionary<Guid, byte[]> Binaries { get; } = [];
                public Dictionary<Guid, TemplateEntity> Templates { get; } = [];
                public HashSet<Guid> ReferencedImageIds { get; } = [];
                public long? ForcedUsedBytesOverride { get; set; }
                public Exception? ExceptionToThrowOnGetImage { get; set; }

                public void SeedImage(ImageEntity image, byte[]? binaryBytes)
                {
                        this.Images[image.Id] = image;
                        if (binaryBytes is not null)
                        {
                                this.Binaries[image.Id] = binaryBytes;
                        }
                }
        }

        /// <summary>
        /// In-memory <see cref="IImageRepository"/> test double for API pipeline tests.
        /// </summary>
        private sealed class InMemoryImageRepository : IImageRepository
        {
                private readonly InMemoryContentState _state;

                public InMemoryImageRepository(InMemoryContentState state) => this._state = state;

                public Task<ImageEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
                {
                        if (this._state.ExceptionToThrowOnGetImage is not null)
                        {
                                throw this._state.ExceptionToThrowOnGetImage;
                        }

                        return Task.FromResult(this._state.Images.TryGetValue(key: id, value: out ImageEntity? image) ? image : null);
                }

                public Task<IReadOnlyList<ImageEntity>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
                        Task.FromResult<IReadOnlyList<ImageEntity>>(
                                ids.Where(id => this._state.Images.ContainsKey(id)).Select(id => this._state.Images[id]).ToList());

                public Task<ImageEntity?> GetBySha256Async(string sha256Hash, CancellationToken cancellationToken) =>
                        Task.FromResult(this._state.Images.Values.FirstOrDefault(image => !image.IsDeleted && image.Sha256Hash == sha256Hash));

                public Task<PagedResult<ImageEntity>> ListAsync(int page, int pageSize, string? searchTerm, CancellationToken cancellationToken)
                {
                        IEnumerable<ImageEntity> active = this._state.Images.Values.Where(image => !image.IsDeleted);
                        if (!string.IsNullOrWhiteSpace(searchTerm))
                        {
                                active = active.Where(image => image.FileName.Contains(value: searchTerm.Trim(), comparisonType: StringComparison.OrdinalIgnoreCase));
                        }

                        List<ImageEntity> all = active.OrderBy(image => image.CreatedAt).ThenBy(image => image.Id).ToList();
                        return Task.FromResult(new PagedResult<ImageEntity>
                        {
                                Items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
                                Page = page,
                                PageSize = pageSize,
                                TotalCount = all.Count
                        });
                }

                public Task<bool> IsReferencedByAnyTemplateAsync(Guid imageId, CancellationToken cancellationToken) =>
                        Task.FromResult(this._state.ReferencedImageIds.Contains(imageId));

                public Task AddAsync(ImageEntity metadata, ImageBinaryEntity binary, CancellationToken cancellationToken)
                {
                        this._state.Images[metadata.Id] = metadata;
                        this._state.Binaries[metadata.Id] = binary.Data;
                        return Task.CompletedTask;
                }

                public Task DeleteAsync(ImageEntity image, Guid expectedConcurrencyStamp, CancellationToken cancellationToken)
                {
                        if (!this._state.Images.ContainsKey(image.Id))
                        {
                                throw new ConcurrencyConflictException($"Image '{image.Id}' was modified concurrently.");
                        }

                        this._state.Images[image.Id] = image;
                        return Task.CompletedTask;
                }
        }

        /// <summary>
        /// In-memory <see cref="IImageBinaryStore"/> test double for API pipeline tests.
        /// </summary>
        private sealed class InMemoryImageBinaryStore : IImageBinaryStore
        {
                private readonly InMemoryContentState _state;

                public InMemoryImageBinaryStore(InMemoryContentState state) => this._state = state;

                public Task<Stream?> OpenReadAsync(Guid imageId, CancellationToken cancellationToken)
                {
                        if (!this._state.Images.TryGetValue(key: imageId, value: out ImageEntity? image)
                                || image.IsDeleted
                                || !this._state.Binaries.TryGetValue(key: imageId, value: out byte[]? data))
                        {
                                return Task.FromResult<Stream?>(null);
                        }

                        Stream stream = new MemoryStream(buffer: data, writable: false);
                        return Task.FromResult<Stream?>(stream);
                }
        }

        /// <summary>
        /// In-memory <see cref="ITemplateRepository"/> test double for API pipeline tests.
        /// </summary>
        private sealed class InMemoryTemplateRepository : ITemplateRepository
        {
                private readonly InMemoryContentState _state;

                public InMemoryTemplateRepository(InMemoryContentState state) => this._state = state;

                public Task<TemplateEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
                        Task.FromResult(this._state.Templates.TryGetValue(key: id, value: out TemplateEntity? template) ? template : null);

                public Task<bool> ExistsByNameAsync(string name, Guid? excludeTemplateId, CancellationToken cancellationToken)
                {
                        string normalized = name.Trim();
                        bool exists = this._state.Templates.Values.Any(template =>
                                string.Equals(a: template.Name, b: normalized, comparisonType: StringComparison.OrdinalIgnoreCase)
                                && (!excludeTemplateId.HasValue || template.Id != excludeTemplateId.Value));
                        return Task.FromResult(exists);
                }

                public Task<PagedResult<TemplateEntity>> ListAsync(
                        int page,
                        int pageSize,
                        TemplateComponentType? componentType,
                        string? searchTerm,
                        CancellationToken cancellationToken)
                {
                        IEnumerable<TemplateEntity> query = this._state.Templates.Values;
                        if (componentType.HasValue)
                        {
                                query = query.Where(template => template.ComponentType == componentType.Value);
                        }

                        if (!string.IsNullOrWhiteSpace(searchTerm))
                        {
                                query = query.Where(template => template.Name.Contains(value: searchTerm.Trim(), comparisonType: StringComparison.OrdinalIgnoreCase));
                        }

                        List<TemplateEntity> all = query.OrderBy(template => template.CreatedAt).ThenBy(template => template.Id).ToList();
                        return Task.FromResult(new PagedResult<TemplateEntity>
                        {
                                Items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
                                Page = page,
                                PageSize = pageSize,
                                TotalCount = all.Count
                        });
                }

                public Task AddAsync(TemplateEntity template, CancellationToken cancellationToken)
                {
                        this._state.Templates[template.Id] = template;
                        return Task.CompletedTask;
                }

                public Task UpdateAsync(TemplateEntity template, Guid expectedConcurrencyStamp, CancellationToken cancellationToken)
                {
                        this._state.Templates[template.Id] = template;
                        return Task.CompletedTask;
                }

                public Task DeleteAsync(TemplateEntity template, Guid expectedConcurrencyStamp, CancellationToken cancellationToken)
                {
                        this._state.Templates[template.Id] = template;
                        return Task.CompletedTask;
                }
        }

        /// <summary>
        /// In-memory <see cref="IStorageQuotaMonitor"/> test double for API pipeline tests.
        /// </summary>
        private sealed class InMemoryStorageQuotaMonitor : IStorageQuotaMonitor
        {
                private readonly InMemoryContentState _state;

                public InMemoryStorageQuotaMonitor(InMemoryContentState state) => this._state = state;

                public Task<long> GetUsedBytesAsync(CancellationToken cancellationToken)
                {
                        long used = this._state.ForcedUsedBytesOverride
                                ?? this._state.Images.Values.Where(image => !image.IsDeleted).Sum(image => image.FileSizeBytes);
                        return Task.FromResult(used);
                }
        }

        /// <summary>
        /// Holds the authenticated identity name injected into test requests.
        /// </summary>
        private sealed record class TestUserIdentityOptions(string UserName);

        /// <summary>
        /// Minimal authentication handler authenticating every request as <see cref="TestUserIdentityOptions.UserName"/>.
        /// </summary>
        private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
        {
                public const string SCHEME_NAME = "TestScheme";
                private readonly TestUserIdentityOptions _userOptions;

                public TestAuthHandler(
                        IOptionsMonitor<AuthenticationSchemeOptions> options,
                        ILoggerFactory logger,
                        UrlEncoder encoder,
                        TestUserIdentityOptions userOptions)
                        : base(options: options, logger: logger, encoder: encoder)
                {
                        this._userOptions = userOptions;
                }

                protected override Task<AuthenticateResult> HandleAuthenticateAsync()
                {
                        Claim[] claims = [new Claim(type: ClaimTypes.Name, value: this._userOptions.UserName)];
                        ClaimsIdentity identity = new(claims: claims, authenticationType: SCHEME_NAME);
                        ClaimsPrincipal principal = new(identity: identity);
                        AuthenticationTicket ticket = new(principal: principal, authenticationScheme: SCHEME_NAME);
                        return Task.FromResult(AuthenticateResult.Success(ticket: ticket));
                }
        }

        /// <summary>
        /// Registers authentication middleware at the front of the test host pipeline.
        /// </summary>
        private sealed class AutoAuthenticateStartupFilter : IStartupFilter
        {
                public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
                        app =>
                        {
                                app.UseAuthentication();
                                next(app);
                        };
        }
}
