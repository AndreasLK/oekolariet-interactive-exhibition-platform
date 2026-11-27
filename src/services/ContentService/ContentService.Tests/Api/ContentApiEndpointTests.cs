using System.Net;
using System.Net.Http.Json;
using ContentService.Api.Contracts;
using ContentService.Domain.Common;
using ContentService.Domain.Entities;
using ContentService.Domain.Enums;
using ContentService.Sdk.Clients;
using ContentService.Sdk.Exceptions;
using ContentService.Sdk.Models;
using ContentService.Sdk.Options;
using ContentService.Tests.Api.Fakes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;

namespace ContentService.Tests.Api;

/// <summary>
/// Fast, Docker-free middleware, CORS, global exception handler, and SDK round-trip tests for <c>ContentService.Api</c>.
/// </summary>
public sealed class ContentApiEndpointTests
{
        /// <summary>
        /// Verifies authenticated identity precedence, CORS fail-closed vs allowed error headers, and 500 redaction in Production vs Development.
        /// </summary>
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
                                ["Cors:AllowedOrigins:0"] = ApiEndpointTestFixtures.ALLOWED_ORIGIN
                        });
                using HttpClient prodClient = prodFactory.CreateClient();

                // Allowed origin gets CORS header even on 500 ProblemDetails, and detail is redacted in Production.
                using HttpRequestMessage allowedOriginRequest = new(method: HttpMethod.Get, requestUri: $"/api/v1/images/{Guid.NewGuid()}");
                allowedOriginRequest.Headers.Add(name: HeaderNames.Origin, value: ApiEndpointTestFixtures.ALLOWED_ORIGIN);
                using HttpResponseMessage prodErrorResponse = await prodClient.SendAsync(request: allowedOriginRequest, cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.InternalServerError, prodErrorResponse.StatusCode);
                Assert.Contains(
                        ApiEndpointTestFixtures.ALLOWED_ORIGIN,
                        prodErrorResponse.Headers.GetValues(HeaderNames.AccessControlAllowOrigin));
                ProblemDetails? prodProblem = await prodErrorResponse.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: TestContext.Current.CancellationToken);
                Assert.DoesNotContain("Sensitive SQL detail", prodProblem?.Detail ?? string.Empty);

                // Disallowed origin must NOT receive Access-Control-Allow-Origin.
                using HttpRequestMessage disallowedOriginRequest = new(method: HttpMethod.Get, requestUri: $"/api/v1/images/{Guid.NewGuid()}");
                disallowedOriginRequest.Headers.Add(name: HeaderNames.Origin, value: ApiEndpointTestFixtures.DISALLOWED_ORIGIN);
                using HttpResponseMessage disallowedResponse = await prodClient.SendAsync(request: disallowedOriginRequest, cancellationToken: TestContext.Current.CancellationToken);
                Assert.False(disallowedResponse.Headers.Contains(HeaderNames.AccessControlAllowOrigin));

                // Development host exposes exception detail on 500 ProblemDetails, plus OpenAPI JSON and Swagger UI.
                await using FakeApiFactory devFactory = new(state: state, environmentName: Environments.Development);
                using HttpClient devClient = devFactory.CreateClient();
                using HttpResponseMessage openApiResponse = await devClient.GetAsync(requestUri: "/openapi/v1.json", cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, openApiResponse.StatusCode);
                string openApiJson = await openApiResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                Assert.Contains("altText", openApiJson);

                using HttpResponseMessage swaggerUiResponse = await devClient.GetAsync(requestUri: "/swagger/index.html", cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, swaggerUiResponse.StatusCode);

                using HttpResponseMessage devErrorResponse = await devClient.GetAsync(requestUri: $"/api/v1/images/{Guid.NewGuid()}", cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.InternalServerError, devErrorResponse.StatusCode);
                ProblemDetails? devProblem = await devErrorResponse.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal("Sensitive SQL detail", devProblem?.Detail);

                // Authenticated User.Identity.Name takes precedence over X-Actor-Id and request body ActorId.
                InMemoryContentState authState = new();
                await using FakeApiFactory authFactory = new(state: authState, authenticatedUserName: "authenticated-curator");
                using HttpClient authClient = authFactory.CreateClient();
                using HttpRequestMessage authCreateRequest = new(method: HttpMethod.Post, requestUri: "/api/v1/templates")
                {
                        Content = JsonContent.Create(
                                inputValue: new CreateTemplateRequest(
                                        Name: "Auth Precedence",
                                        ComponentType: ContentService.Facade.Dtos.TemplateComponentTypeDto.Custom,
                                        SchemaJson: "{}",
                                        ActorId: "spoofed-body-actor"),
                                options: ApiEndpointTestFixtures.API_JSON_OPTIONS)
                };
                authCreateRequest.Headers.Add(name: "X-Actor-Id", value: "spoofed-header-actor");
                using HttpResponseMessage authCreateResponse = await authClient.SendAsync(request: authCreateRequest, cancellationToken: TestContext.Current.CancellationToken);
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
                await using MemoryStream uploadStream = new(buffer: ApiEndpointTestFixtures.VALID_PNG_BYTES);
                ContentImageMetadata uploaded = await sdkClient.UploadImageAsync(request: new UploadImageRequest
                {
                        Content = uploadStream,
                        FileName = "økolariet æble.png",
                        ContentType = DomainConstants.CONTENT_TYPE_PNG,
                        AltText = "Økolariet logo"
                }, cancellationToken: TestContext.Current.CancellationToken);

                Assert.Equal(expected: "økolariet æble.png", actual: uploaded.FileName);
                Assert.Equal(expected: DomainConstants.CONTENT_TYPE_PNG, actual: uploaded.ContentType);
                Assert.Equal(expected: ApiEndpointTestFixtures.VALID_PNG_BYTES.Length, actual: uploaded.SizeBytes);

                // Verify metadata retrieval and content URI generation match the uploaded image.
                ContentImageMetadata fetched = await sdkClient.GetImageMetadataAsync(imageId: uploaded.Id, cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(expected: uploaded.Id, actual: fetched.Id);
                Assert.Equal(
                        expected: new Uri(baseUri: httpClient.BaseAddress!, relativeUri: $"api/v1/images/{uploaded.Id:D}/content"),
                        actual: sdkClient.GetImageContentUri(imageId: uploaded.Id));

                // Verify download extracts FileNameStar and streams exact PNG bytes.
                await using ContentImageDownload downloaded = await sdkClient.DownloadImageAsync(imageId: uploaded.Id, cancellationToken: TestContext.Current.CancellationToken);
                await using MemoryStream downloadedBytes = new();
                await downloaded.Content.CopyToAsync(destination: downloadedBytes, cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(expected: ApiEndpointTestFixtures.VALID_PNG_BYTES, actual: downloadedBytes.ToArray());
                Assert.Equal(expected: "økolariet æble.png", actual: downloaded.FileName);
                Assert.Equal(expected: DomainConstants.CONTENT_TYPE_PNG, actual: downloaded.ContentType);

                // Verify OpenAPI documents 415 on POST /api/v1/images and SDK maps 404 ProblemDetails cleanly.
                string openApiJson = await httpClient.GetStringAsync(requestUri: "/openapi/v1.json", cancellationToken: TestContext.Current.CancellationToken);
                Assert.Contains(expectedSubstring: "\"415\"", actualString: openApiJson);

                ContentServiceApiException notFound = await Assert.ThrowsAsync<ContentServiceApiException>(
                        testCode: () => sdkClient.GetImageMetadataAsync(imageId: Guid.NewGuid(), cancellationToken: TestContext.Current.CancellationToken));
                Assert.Equal(expected: HttpStatusCode.NotFound, actual: notFound.StatusCode);
                Assert.NotNull(notFound.ProblemDetails);
        }
}
