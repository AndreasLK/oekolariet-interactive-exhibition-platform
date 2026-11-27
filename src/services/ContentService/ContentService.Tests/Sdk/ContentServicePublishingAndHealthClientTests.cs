using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ContentService.Sdk;
using ContentService.Sdk.Clients;
using ContentService.Sdk.Models;
using ContentService.Sdk.Options;
using ContentService.Sdk.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace ContentService.Tests.Sdk;

/// <summary>
/// Verifies <see cref="IContentServicePlatformClient"/>,
/// <see cref="IContentServiceImageClient.DownloadImagePreviewAsync(Guid, CancellationToken)"/>, and SDK DI registration.
/// </summary>
public sealed class ContentServicePublishingAndHealthClientTests
{
        private static readonly Uri BASE_ADDRESS = new(uriString: "https://content.oekolariet.local/subpath/", uriKind: UriKind.Absolute);

        /// <summary>
        /// Verifies <see cref="DependencyInjection.AddContentServiceSdk(IServiceCollection, Action{ContentServiceClientOptions})"/>
        /// registers all typed SDK clients needed by consumers such as EditorService.
        /// </summary>
        [Fact]
        public void AddContentServiceSdkRegistersAllTypedClients()
        {
                ServiceCollection services = new();
                services.AddContentServiceSdk(options => options.BaseAddress = BASE_ADDRESS);

                using ServiceProvider provider = services.BuildServiceProvider();

                Assert.NotNull(provider.GetService<IContentServiceImageClient>());
                Assert.NotNull(provider.GetService<IContentServiceTemplateClient>());
                Assert.NotNull(provider.GetService<IContentServicePlatformClient>());
        }

        /// <summary>
        /// Verifies <see cref="IContentServicePlatformClient.ResolvePublishBundleAsync"/> posts to
        /// <c>api/v1/publish-bundles/resolve</c> and deserializes assets and bundle checksum.
        /// </summary>
        [Fact]
        public async Task ResolvePublishBundleAsyncPostsPayloadAndDeserializesBundle()
        {
                Guid templateId = Guid.NewGuid();
                Guid imageId = Guid.NewGuid();
                string responseJson = JsonSerializer.Serialize(new
                {
                        TemplateId = templateId,
                        TemplateSchemaJson = "{\"title\":\"Quiz\"}",
                        ImplementationJson = "{\"answers\":[]}",
                        Assets = new[]
                        {
                                new
                                {
                                        ImageId = imageId,
                                        PictureReference = $"picture.node1.{imageId:D}",
                                        ExternalId = $"node1_{imageId:D}",
                                        ContentType = "image/png",
                                        SizeBytes = 1234L,
                                        Sha256Hash = new string(c: 'a', count: 64)
                                }
                        },
                        BundleChecksum = new string(c: 'b', count: 64)
                });

                StubHttpMessageHandler handler = new(request =>
                {
                        Assert.Equal(expected: HttpMethod.Post, actual: request.Method);
                        Assert.Equal(
                                expected: new Uri(baseUri: BASE_ADDRESS, relativeUri: "api/v1/publish-bundles/resolve"),
                                actual: request.RequestUri);

                        return new HttpResponseMessage(statusCode: HttpStatusCode.OK)
                        {
                                Content = new StringContent(
                                        content: responseJson,
                                        encoding: Encoding.UTF8,
                                        mediaType: "application/json")
                        };
                });

                ServiceCollection services = new();
                services.AddContentServiceSdk(options => options.BaseAddress = BASE_ADDRESS);
                services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(() => handler));
                using ServiceProvider provider = services.BuildServiceProvider();
                IContentServicePlatformClient client = provider.GetRequiredService<IContentServicePlatformClient>();

                ContentPublishBundleResponse bundle = await client.ResolvePublishBundleAsync(
                        query: new ContentResolvePublishBundleQuery
                        {
                                TemplateId = templateId,
                                ImplementationJson = "{\"answers\":[]}"
                        },
                        cancellationToken: TestContext.Current.CancellationToken);

                Assert.Equal(expected: templateId, actual: bundle.TemplateId);
                Assert.Single(collection: bundle.Assets);
                Assert.Equal(expected: imageId, actual: bundle.Assets[0].ImageId);
                Assert.Equal(expected: new string(c: 'b', count: 64), actual: bundle.BundleChecksum);
        }

        /// <summary>
        /// Verifies <see cref="IContentServicePlatformClient.GetStorageHealthAsync"/> requests
        /// <c>api/v1/health/storage</c> and deserializes stringified <see cref="ContentStorageHealthStatus"/>.
        /// </summary>
        [Fact]
        public async Task GetStorageHealthAsyncRequestsHealthEndpointAndDeserializesStatus()
        {
                const string RESPONSE_JSON = """
                        {
                          "status": "Warning",
                          "usedBytes": 800,
                          "quotaBytes": 1000,
                          "utilizationPercent": 80.0
                        }
                        """;

                StubHttpMessageHandler handler = new(request =>
                {
                        Assert.Equal(expected: HttpMethod.Get, actual: request.Method);
                        Assert.Equal(
                                expected: new Uri(baseUri: BASE_ADDRESS, relativeUri: "api/v1/health/storage"),
                                actual: request.RequestUri);

                        return new HttpResponseMessage(statusCode: HttpStatusCode.OK)
                        {
                                Content = new StringContent(
                                        content: RESPONSE_JSON,
                                        encoding: Encoding.UTF8,
                                        mediaType: "application/json")
                        };
                });

                ServiceCollection services = new();
                services.AddContentServiceSdk(options => options.BaseAddress = BASE_ADDRESS);
                services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(() => handler));
                using ServiceProvider provider = services.BuildServiceProvider();
                IContentServicePlatformClient client = provider.GetRequiredService<IContentServicePlatformClient>();

                ContentStorageHealthResponse report = await client.GetStorageHealthAsync(
                        cancellationToken: TestContext.Current.CancellationToken);

                Assert.Equal(expected: ContentStorageHealthStatus.Warning, actual: report.Status);
                Assert.Equal(expected: 800L, actual: report.UsedBytes);
                Assert.Equal(expected: 1000L, actual: report.QuotaBytes);
                Assert.Equal(expected: 80.0d, actual: report.UtilizationPercent);
        }

        /// <summary>
        /// Verifies <see cref="IContentServiceImageClient.DownloadImagePreviewAsync(Guid, CancellationToken)"/> streams
        /// from <c>api/v1/images/{id}/preview</c> and transfers stream ownership to <see cref="ContentImageDownloadResponse"/>.
        /// </summary>
        [Fact]
        public async Task DownloadImagePreviewAsyncStreamsFromPreviewEndpoint()
        {
                Guid imageId = Guid.NewGuid();
                byte[] previewBytes = [1, 2, 3, 4];

                StubHttpMessageHandler handler = new(request =>
                {
                        Assert.Equal(expected: HttpMethod.Get, actual: request.Method);
                        Assert.Equal(
                                expected: new Uri(baseUri: BASE_ADDRESS, relativeUri: $"api/v1/images/{imageId:D}/preview"),
                                actual: request.RequestUri);

                        ByteArrayContent content = new(content: previewBytes);
                        content.Headers.ContentType = new MediaTypeHeaderValue(mediaType: "image/webp");
                        content.Headers.ContentLength = previewBytes.Length;

                        return new HttpResponseMessage(statusCode: HttpStatusCode.OK)
                        {
                                Content = content
                        };
                });

                ServiceCollection services = new();
                services.AddContentServiceSdk(options => options.BaseAddress = BASE_ADDRESS);
                services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(() => handler));
                using ServiceProvider provider = services.BuildServiceProvider();
                IContentServiceImageClient client = provider.GetRequiredService<IContentServiceImageClient>();

                await using ContentImageDownloadResponse download = await client.DownloadImagePreviewAsync(
                        imageId: imageId,
                        cancellationToken: TestContext.Current.CancellationToken);

                using MemoryStream buffer = new();
                await download.Content.CopyToAsync(
                        destination: buffer,
                        cancellationToken: TestContext.Current.CancellationToken);

                Assert.Equal(expected: "image/webp", actual: download.ContentType);
                Assert.Equal(expected: previewBytes, actual: buffer.ToArray());
        }

        private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
        {
                protected override Task<HttpResponseMessage> SendAsync(
                        HttpRequestMessage request,
                        CancellationToken cancellationToken) =>
                        Task.FromResult(result: responder(request));
        }
}

