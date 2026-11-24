using System.Net;
using System.Net.Http.Json;
using ContentService.Api.Contracts;
using ContentService.Application.Models;
using ContentService.Domain.Entities;
using ContentService.Domain.Enums;
using ContentService.Tests.Api.Fakes;

namespace ContentService.Tests.Api;

/// <summary>
/// Fast, Docker-free HTTP endpoint tests covering template CRUD, publish bundle resolution, and storage health metrics.
/// </summary>
public sealed class TemplateAndHealthEndpointTests
{
        /// <summary>
        /// Verifies template CRUD endpoints, query enum errors, concurrency conflicts, publish bundle resolution, and storage health thresholds.
        /// </summary>
        [Fact]
        public async Task TemplatePublishingAndHealthEndpoints_WhenExercisedAcrossEdgeCases_ReturnExpectedResponsesAsync()
        {
                // Arrange
                InMemoryContentState state = new();
                ImageEntity image = ApiEndpointTestFixtures.CreateSeededImage(
                        fileName: "asset.png",
                        bytes: ApiEndpointTestFixtures.VALID_PNG_BYTES);
                state.SeedImage(image: image, binaryBytes: ApiEndpointTestFixtures.VALID_PNG_BYTES);

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
                        ComponentType: ContentService.Facade.Dtos.TemplateComponentTypeDto.QuizTemplate,
                        SchemaJson: $"{{\"img\":\"{image.Reference}\"}}");
                using HttpResponseMessage createResponse = await client.PostAsJsonAsync(requestUri: "/api/templates", value: createRequest, options: ApiEndpointTestFixtures.API_JSON_OPTIONS, cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
                TemplateResult? created = await createResponse.Content.ReadFromJsonAsync<TemplateResult>(options: ApiEndpointTestFixtures.API_JSON_OPTIONS, cancellationToken: TestContext.Current.CancellationToken);
                Assert.NotNull(created);

                using HttpResponseMessage duplicateResponse = await client.PostAsJsonAsync(requestUri: "/api/templates", value: createRequest, options: ApiEndpointTestFixtures.API_JSON_OPTIONS, cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.BadRequest, duplicateResponse.StatusCode);

                using HttpResponseMessage invalidEnumListResponse = await client.GetAsync(requestUri: "/api/templates?componentType=NotARealType", cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.BadRequest, invalidEnumListResponse.StatusCode);

                // Update template: missing -> 404; empty stamp -> 409; valid -> 200; stale stamp -> 409.
                using HttpResponseMessage missingPutResponse = await client.PutAsJsonAsync(requestUri: $"/api/templates/{Guid.NewGuid()}", value: new UpdateTemplateRequest(Name: "New", SchemaJson: "{}", ExpectedConcurrencyStamp: Guid.NewGuid()), options: ApiEndpointTestFixtures.API_JSON_OPTIONS, cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.NotFound, missingPutResponse.StatusCode);

                using HttpResponseMessage emptyStampPutResponse = await client.PutAsJsonAsync(requestUri: $"/api/templates/{created.Id}", value: new UpdateTemplateRequest(Name: "New", SchemaJson: "{}", ExpectedConcurrencyStamp: Guid.Empty), options: ApiEndpointTestFixtures.API_JSON_OPTIONS, cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.Conflict, emptyStampPutResponse.StatusCode);

                using HttpResponseMessage updateResponse = await client.PutAsJsonAsync(requestUri: $"/api/templates/{created.Id}", value: new UpdateTemplateRequest(
                                Name: "Quiz Card v2",
                                SchemaJson: $"{{\"img\":\"{image.Reference}\"}}",
                                ExpectedConcurrencyStamp: created.ConcurrencyStamp), options: ApiEndpointTestFixtures.API_JSON_OPTIONS, cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

                using HttpResponseMessage staleUpdateResponse = await client.PutAsJsonAsync(requestUri: $"/api/templates/{created.Id}", value: new UpdateTemplateRequest(
                                Name: "Quiz Card stale",
                                SchemaJson: "{}",
                                ExpectedConcurrencyStamp: created.ConcurrencyStamp), options: ApiEndpointTestFixtures.API_JSON_OPTIONS, cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.Conflict, staleUpdateResponse.StatusCode);

                // Archive template -> 204; repeat delete -> 404; publish bundle with archived template -> 200.
                using HttpResponseMessage deleteTemplateResponse = await client.DeleteAsync(requestUri: $"/api/templates/{created.Id}", cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.NoContent, deleteTemplateResponse.StatusCode);

                using HttpResponseMessage repeatDeleteResponse = await client.DeleteAsync(requestUri: $"/api/templates/{created.Id}", cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.NotFound, repeatDeleteResponse.StatusCode);

                using HttpResponseMessage publishResponse = await client.PostAsJsonAsync(requestUri: "/api/publish-bundles/resolve", value: new ResolvePublishBundleCommand { TemplateId = created.Id, ImplementationJson = "{}" }, options: ApiEndpointTestFixtures.API_JSON_OPTIONS, cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);

                // Publish bundle with Guid.Empty -> 400.
                using HttpResponseMessage emptyPublishResponse = await client.PostAsJsonAsync(requestUri: "/api/publish-bundles/resolve", value: new ResolvePublishBundleCommand { TemplateId = Guid.Empty, ImplementationJson = "{}" }, options: ApiEndpointTestFixtures.API_JSON_OPTIONS, cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.BadRequest, emptyPublishResponse.StatusCode);

                // Health endpoint transitions: Healthy (<500), Warning (600), FullReadOnly (950).
                state.ForcedUsedBytesOverride = 200L;
                StorageHealthResult? healthy = await client.GetFromJsonAsync<StorageHealthResult>(requestUri: "/api/health/storage", options: ApiEndpointTestFixtures.API_JSON_OPTIONS, cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(StorageHealthStatus.Healthy, healthy?.Status);

                state.ForcedUsedBytesOverride = 600L;
                StorageHealthResult? warning = await client.GetFromJsonAsync<StorageHealthResult>(requestUri: "/api/health/storage", options: ApiEndpointTestFixtures.API_JSON_OPTIONS, cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(StorageHealthStatus.Warning, warning?.Status);

                state.ForcedUsedBytesOverride = 950L;
                StorageHealthResult? full = await client.GetFromJsonAsync<StorageHealthResult>(requestUri: "/api/health/storage", options: ApiEndpointTestFixtures.API_JSON_OPTIONS, cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(StorageHealthStatus.FullReadOnly, full?.Status);
        }
}

