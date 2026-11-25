using System.Net;
using System.Net.Http.Json;
using System.Text;
using ContentService.Api.Contracts;
using ContentService.Application.Common;
using ContentService.Application.Models;
using ContentService.Domain.Common;
using ContentService.Domain.Enums;
using ContentService.Tests.Application.Persistence;
using Microsoft.Net.Http.Headers;

namespace ContentService.Tests.Api;

/// <summary>
/// End-to-end HTTP integration tests verifying <c>/api/v1/images</c> upload, streaming, caching headers, conflict guards, and SQL Server persistence.
/// </summary>
[Collection(MsSqlContentDatabaseCollection.COLLECTION_NAME)]
public sealed class ImageApiIntegrationTests : IAsyncLifetime
{
        private const string EXPECTED_NOSNIFF_HEADER = "nosniff";
        private const string EXPECTED_ACCEPT_RANGES_HEADER = "none";
        private const string EXPECTED_CACHE_CONTROL_HEADER = "public, max-age=31536000, immutable";
        private const string EXPECTED_INLINE_DISPOSITION = "inline";
        private const int SMALL_MAX_IMAGE_BYTES = 64;
        private const int OVERSIZED_PNG_BYTES = 128;
        private const int TIGHT_QUOTA_BYTES = 8;

        private readonly MsSqlContentDatabaseFixture _fixture;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageApiIntegrationTests"/> class.
        /// </summary>
        /// <param name="fixture">The shared SQL Server container fixture.</param>
        public ImageApiIntegrationTests(MsSqlContentDatabaseFixture fixture)
        {
                ArgumentNullException.ThrowIfNull(fixture);
                this._fixture = fixture;
        }

        /// <inheritdoc />
        public ValueTask InitializeAsync() => new(this._fixture.ResetDatabaseAsync());

        /// <inheritdoc />
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        /// <summary>
        /// Verifies the full HTTP image lifecycle: upload (201), metadata (200), list (200), content stream + immutable <c>Cache-Control</c> (200), and delete (204 -> 404).
        /// </summary>
        [Fact]
        public async Task ImagesApi_FullLifecycle_UploadsStreamsCachesAndDeletesCorrectlyAsync()
        {
                // Arrange
                using ContentApiWebApplicationFactory factory = new(connectionString: this._fixture.ConnectionString);
                using HttpClient client = factory.CreateClient();

                // Act 1: Upload valid PNG with parameterized Content-Type header to verify media type extraction.
                using HttpResponseMessage uploadResponse = await ApiIntegrationTestFixtures.UploadImageAsync(
                        client: client,
                        payload: ApiIntegrationTestFixtures.VALID_PNG_PAYLOAD,
                        fileName: "exhibit-hero.png",
                        contentType: $"{DomainConstants.CONTENT_TYPE_PNG}; charset=binary",
                        altText: "Hero photo");

                // Assert 1
                Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
                ImageMetadataResult? uploaded = await uploadResponse.Content.ReadFromJsonAsync<ImageMetadataResult>(options: ApiIntegrationTestFixtures.API_JSON_OPTIONS, cancellationToken: TestContext.Current.CancellationToken);
                Assert.NotNull(uploaded);
                Assert.Equal($"{ApiIntegrationTestFixtures.IMAGES_ROUTE}/{uploaded.ExternalId}", uploadResponse.Headers.Location?.ToString());
                Assert.Equal("exhibit-hero.png", uploaded.FileName);
                Assert.Equal(DomainConstants.CONTENT_TYPE_PNG, uploaded.ContentType);
                Assert.Equal("Hero photo", uploaded.AltText);
                Assert.Equal(ApiIntegrationTestFixtures.VALID_PNG_PAYLOAD.Length, uploaded.SizeBytes);

                // Act 2: Metadata + List
                using HttpResponseMessage metadataResponse = await client.GetAsync($"{ApiIntegrationTestFixtures.IMAGES_ROUTE}/{uploaded.Id}", TestContext.Current.CancellationToken);
                using HttpResponseMessage listResponse = await client.GetAsync($"{ApiIntegrationTestFixtures.IMAGES_ROUTE}?page=1&pageSize=10&searchTerm=hero", TestContext.Current.CancellationToken);

                // Assert 2
                Assert.Equal(HttpStatusCode.OK, metadataResponse.StatusCode);
                Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
                PagedResult<ImageMetadataResult>? listed = await listResponse.Content.ReadFromJsonAsync<PagedResult<ImageMetadataResult>>(options: ApiIntegrationTestFixtures.API_JSON_OPTIONS, cancellationToken: TestContext.Current.CancellationToken);
                Assert.NotNull(listed);
                Assert.Single(listed.Items);

                // Act 3: Stream content
                using HttpResponseMessage contentResponse = await client.GetAsync($"{ApiIntegrationTestFixtures.IMAGES_ROUTE}/{uploaded.Id}/content", TestContext.Current.CancellationToken);
                byte[] downloadedBytes = await contentResponse.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);

                // Assert 3
                Assert.Equal(HttpStatusCode.OK, contentResponse.StatusCode);
                Assert.True(contentResponse.Headers.CacheControl?.Public);
                Assert.Equal(
                        EXPECTED_CACHE_CONTROL_HEADER,
                        contentResponse.Headers.GetValues(HeaderNames.CacheControl).Single());
                Assert.Equal(EXPECTED_NOSNIFF_HEADER, contentResponse.Headers.GetValues(HeaderNames.XContentTypeOptions).Single());
                Assert.Equal(EXPECTED_ACCEPT_RANGES_HEADER, contentResponse.Headers.AcceptRanges.Single());
                Assert.Equal(EXPECTED_INLINE_DISPOSITION, contentResponse.Content.Headers.ContentDisposition?.DispositionType);
                Assert.Equal(ApiIntegrationTestFixtures.VALID_PNG_PAYLOAD.Length, contentResponse.Content.Headers.ContentLength);
                Assert.Equal(ApiIntegrationTestFixtures.VALID_PNG_PAYLOAD, downloadedBytes);

                // Act 4: Delete unreferenced image -> 204 -> 404
                using HttpResponseMessage deleteResponse = await client.DeleteAsync($"{ApiIntegrationTestFixtures.IMAGES_ROUTE}/{uploaded.Id}", TestContext.Current.CancellationToken);
                using HttpResponseMessage afterDeleteMetadata = await client.GetAsync($"{ApiIntegrationTestFixtures.IMAGES_ROUTE}/{uploaded.Id}", TestContext.Current.CancellationToken);
                using HttpResponseMessage afterDeleteContent = await client.GetAsync($"{ApiIntegrationTestFixtures.IMAGES_ROUTE}/{uploaded.Id}/content", TestContext.Current.CancellationToken);

                // Assert 4
                Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, afterDeleteMetadata.StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, afterDeleteContent.StatusCode);
        }

        /// <summary>
        /// Verifies that spoofed magic bytes, oversized image uploads, and quota exhaustion map to 400 and 507 ProblemDetails responses.
        /// </summary>
        [Fact]
        public async Task ImagesApi_WhenInvalidMagicBytesOversizedOrQuotaExceeded_ReturnsExpectedProblemDetailsAsync()
        {
                // Arrange: tight MaxImageSizeBytes = 64
                Dictionary<string, string?> sizeOverrides = new()
                {
                        [ApiIntegrationTestFixtures.MAX_IMAGE_SIZE_CONFIG_KEY] = SMALL_MAX_IMAGE_BYTES.ToString()
                };
                using ContentApiWebApplicationFactory sizeFactory = new(
                        connectionString: this._fixture.ConnectionString,
                        configurationOverrides: sizeOverrides);
                using HttpClient sizeClient = sizeFactory.CreateClient();

                byte[] spoofedSvg = Encoding.UTF8.GetBytes("<svg><script>alert(1)</script></svg>");
                byte[] oversizedPng = new byte[OVERSIZED_PNG_BYTES];
                Array.Copy(
                        sourceArray: ApiIntegrationTestFixtures.VALID_PNG_PAYLOAD,
                        destinationArray: oversizedPng,
                        length: ApiIntegrationTestFixtures.VALID_PNG_PAYLOAD.Length);

                // Act 1 & 2: Invalid magic bytes & oversized upload
                using HttpResponseMessage spoofedResponse = await ApiIntegrationTestFixtures.UploadImageAsync(
                        client: sizeClient,
                        payload: spoofedSvg,
                        fileName: "spoofed.png",
                        contentType: DomainConstants.CONTENT_TYPE_PNG);
                using HttpResponseMessage oversizedResponse = await ApiIntegrationTestFixtures.UploadImageAsync(
                        client: sizeClient,
                        payload: oversizedPng,
                        fileName: "too-big.png",
                        contentType: DomainConstants.CONTENT_TYPE_PNG);

                // Assert 1 & 2
                await ApiIntegrationTestFixtures.AssertProblemDetailsStatusAsync(
                        response: spoofedResponse,
                        expectedStatusCode: HttpStatusCode.BadRequest);
                await ApiIntegrationTestFixtures.AssertProblemDetailsStatusAsync(
                        response: oversizedResponse,
                        expectedStatusCode: HttpStatusCode.BadRequest);

                // Arrange: tight quota = 8 bytes
                Dictionary<string, string?> quotaOverrides = new()
                {
                        [ApiIntegrationTestFixtures.TOTAL_QUOTA_CONFIG_KEY] = TIGHT_QUOTA_BYTES.ToString()
                };
                using ContentApiWebApplicationFactory quotaFactory = new(
                        connectionString: this._fixture.ConnectionString,
                        configurationOverrides: quotaOverrides);
                using HttpClient quotaClient = quotaFactory.CreateClient();

                // Act 3: Quota exceeded
                using HttpResponseMessage quotaResponse = await ApiIntegrationTestFixtures.UploadImageAsync(
                        client: quotaClient,
                        payload: ApiIntegrationTestFixtures.VALID_PNG_PAYLOAD,
                        fileName: "over-quota.png",
                        contentType: DomainConstants.CONTENT_TYPE_PNG);

                // Assert 3
                await ApiIntegrationTestFixtures.AssertProblemDetailsStatusAsync(
                        response: quotaResponse,
                        expectedStatusCode: HttpStatusCode.InsufficientStorage);
        }

        /// <summary>
        /// Verifies that deleting an image referenced by a template returns <c>409 Conflict</c> with <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/>.
        /// </summary>
        [Fact]
        public async Task DeleteImageApi_WhenReferencedByTemplate_Returns409ConflictProblemDetailsAsync()
        {
                // Arrange
                using ContentApiWebApplicationFactory factory = new(connectionString: this._fixture.ConnectionString);
                using HttpClient client = factory.CreateClient();

                using HttpResponseMessage uploadResponse = await ApiIntegrationTestFixtures.UploadImageAsync(
                        client: client,
                        payload: ApiIntegrationTestFixtures.VALID_PNG_PAYLOAD,
                        fileName: "linked.png",
                        contentType: DomainConstants.CONTENT_TYPE_PNG);
                ImageMetadataResult? image = await uploadResponse.Content.ReadFromJsonAsync<ImageMetadataResult>(options: ApiIntegrationTestFixtures.API_JSON_OPTIONS, cancellationToken: TestContext.Current.CancellationToken);
                Assert.NotNull(image);

                CreateTemplateRequest createTemplate = new(
                        Name: "Referencing Template",
                        ComponentType: ContentService.Facade.Dtos.TemplateComponentTypeDto.QuizTemplate,
                        SchemaJson: $"{{\"hero\":\"{image.PictureReference}\"}}");
                using HttpResponseMessage templateResponse = await client.PostAsJsonAsync(requestUri: ApiIntegrationTestFixtures.TEMPLATES_ROUTE, value: createTemplate, options: ApiIntegrationTestFixtures.API_JSON_OPTIONS, cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.Created, templateResponse.StatusCode);

                // Act
                using HttpResponseMessage deleteResponse = await client.DeleteAsync($"{ApiIntegrationTestFixtures.IMAGES_ROUTE}/{image.Id}", TestContext.Current.CancellationToken);

                // Assert
                await ApiIntegrationTestFixtures.AssertProblemDetailsStatusAsync(
                        response: deleteResponse,
                        expectedStatusCode: HttpStatusCode.Conflict);
        }
}

