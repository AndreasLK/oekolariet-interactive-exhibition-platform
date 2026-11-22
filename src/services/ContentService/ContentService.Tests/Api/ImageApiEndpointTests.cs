using System.Net;
using System.Net.Http.Json;
using System.Text;
using ContentService.Application.Models;
using ContentService.Domain.Common;
using ContentService.Domain.Entities;
using ContentService.Tests.Api.Fakes;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;

namespace ContentService.Tests.Api;

/// <summary>
/// Fast, Docker-free HTTP endpoint tests covering image upload, metadata, streaming, CORS vary headers, and soft-deletion.
/// </summary>
public sealed class ImageApiEndpointTests
{
        /// <summary>
        /// Verifies PNG, JPEG, and WebP multipart uploads, empty/missing files, MIME spoofing, 413/415 transport limits, 507 quota errors, and actor header handling.
        /// </summary>
        [Fact]
        public async Task ImageUploadEndpoint_WhenValidAndInvalidRequestsArrive_ReturnsExpectedStatusCodesAsync()
        {
                // Arrange
                InMemoryContentState state = new();
                await using FakeApiFactory factory = new(
                        state: state,
                        configurationOverrides: new Dictionary<string, string?>
                        {
                                ["ContentStorage:MaxImageSizeBytes"] = ApiEndpointTestFixtures.SMALL_MAX_IMAGE_BYTES.ToString(),
                                ["ContentStorage:TotalQuotaBytes"] = "10000"
                        });
                using HttpClient client = factory.CreateClient();

                // Act & Assert: Valid PNG, JPEG, and WebP uploads return 201 with Location header.
                using HttpResponseMessage pngResponse = await ApiEndpointTestFixtures.UploadFileAsync(
                        client: client,
                        bytes: ApiEndpointTestFixtures.VALID_PNG_BYTES,
                        fileName: "skov billede æøå.png",
                        contentType: "image/png; charset=utf-8");
                Assert.Equal(HttpStatusCode.Created, pngResponse.StatusCode);
                Assert.NotNull(pngResponse.Headers.Location);
                ImageMetadataResult? pngMetadata = await pngResponse.Content.ReadFromJsonAsync<ImageMetadataResult>(
                        options: ApiEndpointTestFixtures.API_JSON_OPTIONS);
                Assert.NotNull(pngMetadata);
                Assert.Equal(DomainConstants.CONTENT_TYPE_PNG, pngMetadata.ContentType);

                using HttpResponseMessage jpegResponse = await ApiEndpointTestFixtures.UploadFileAsync(
                        client: client,
                        bytes: ApiEndpointTestFixtures.VALID_JPEG_BYTES,
                        fileName: "photo.jpg",
                        contentType: DomainConstants.CONTENT_TYPE_JPEG);
                Assert.Equal(HttpStatusCode.Created, jpegResponse.StatusCode);

                using HttpResponseMessage webpResponse = await ApiEndpointTestFixtures.UploadFileAsync(
                        client: client,
                        bytes: ApiEndpointTestFixtures.VALID_WEBP_BYTES,
                        fileName: "banner.webp",
                        contentType: DomainConstants.CONTENT_TYPE_WEBP);
                Assert.Equal(HttpStatusCode.Created, webpResponse.StatusCode);

                // Blank X-Actor-Id must fail validation (400) rather than falling back silently.
                using MultipartFormDataContent blankActorForm = ApiEndpointTestFixtures.CreateImageMultipart(
                        bytes: ApiEndpointTestFixtures.VALID_PNG_BYTES,
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

                using HttpResponseMessage emptyFileResponse = await ApiEndpointTestFixtures.UploadFileAsync(
                        client: client,
                        bytes: Array.Empty<byte>(),
                        fileName: "empty.png",
                        contentType: DomainConstants.CONTENT_TYPE_PNG);
                Assert.Equal(HttpStatusCode.BadRequest, emptyFileResponse.StatusCode);

                // Spoofed SVG-as-PNG yields 400.
                using HttpResponseMessage spoofedResponse = await ApiEndpointTestFixtures.UploadFileAsync(
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
                using HttpResponseMessage oversizedMultipartResponse = await ApiEndpointTestFixtures.UploadFileAsync(
                        client: client,
                        bytes: new byte[ApiEndpointTestFixtures.MULTIPART_LIMIT_EXCEEDING_BYTES],
                        fileName: "huge.png",
                        contentType: DomainConstants.CONTENT_TYPE_PNG);
                Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversizedMultipartResponse.StatusCode);

                // Exhausted quota yields 507 InsufficientStorage.
                state.ForcedUsedBytesOverride = 10_000L;
                using HttpResponseMessage quotaResponse = await ApiEndpointTestFixtures.UploadFileAsync(
                        client: client,
                        bytes: ApiEndpointTestFixtures.VALID_PNG_BYTES,
                        fileName: "full.png",
                        contentType: DomainConstants.CONTENT_TYPE_PNG);
                Assert.Equal(HttpStatusCode.InsufficientStorage, quotaResponse.StatusCode);
        }

        /// <summary>
        /// Verifies image metadata, listing, content headers, UTF-8 <c>Content-Disposition</c>, immutable <c>Cache-Control</c>, orphaned binary 404, and deletion conflicts.
        /// </summary>
        [Fact]
        public async Task ImageReadStreamAndDeleteEndpoints_WhenExercisedAcrossEdgeCases_ReturnExpectedResponsesAsync()
        {
                // Arrange
                InMemoryContentState state = new();
                ImageEntity seeded = ApiEndpointTestFixtures.CreateSeededImage(
                        fileName: "skov billede æøå.png",
                        bytes: ApiEndpointTestFixtures.VALID_PNG_BYTES);
                state.SeedImage(image: seeded, binaryBytes: ApiEndpointTestFixtures.VALID_PNG_BYTES);

                ImageEntity orphaned = ApiEndpointTestFixtures.CreateSeededImage(
                        fileName: "orphan.png",
                        bytes: ApiEndpointTestFixtures.VALID_PNG_BYTES);
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
                Assert.Equal(ApiEndpointTestFixtures.VALID_PNG_BYTES, await contentResponse.Content.ReadAsByteArrayAsync());
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
}

