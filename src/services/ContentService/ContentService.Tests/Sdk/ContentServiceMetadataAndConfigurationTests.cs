using System.Net;
using System.Text;
using System.Text.Json;
using ContentService.Sdk;
using ContentService.Sdk.Clients;
using ContentService.Sdk.Models;
using ContentService.Sdk.Options;
using ContentService.Tests.Sdk.TestDoubles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ContentService.Tests.Sdk;

/// <summary>
/// Unit tests verifying metadata retrieval, subpath URI normalization, and <c>AddContentServiceSdk</c> configuration validation.
/// </summary>
public sealed class ContentServiceMetadataAndConfigurationTests
{
        private const string BASE_URL_WITH_SUBPATH = "https://content.oekolariet.dk/content";
        private const string DEFAULT_ACTOR_ID = "editor-default";

        /// <summary>
        /// Verifies that <see cref="ContentServiceImageClient.GetImageMetadataAsync"/> queries the normalized metadata route.
        /// </summary>
        [Fact]
        public async Task GetImageMetadataAsync_WhenValid_RequestsNormalizedRouteAndDeserializesMetadataAsync()
        {
                Guid imageId = Guid.NewGuid();
                ContentImageMetadata expected = new()
                {
                        Id = imageId,
                        PictureReference = $"picture.node-1.{imageId:D}",
                        ExternalId = $"node-1_{imageId:D}",
                        FileName = "exhibit.png",
                        ContentType = "image/png",
                        SizeBytes = 64,
                        Sha256Hash = new string(c: 'b', count: 64),
                        AltText = "Exhibit",
                        CreatedAt = DateTimeOffset.UtcNow,
                        ConcurrencyStamp = Guid.NewGuid()
                };

                StubHttpMessageHandler handler = new(responder: (_, _) => Task.FromResult(new HttpResponseMessage(statusCode: HttpStatusCode.OK)
                {
                        Content = new StringContent(
                                content: JsonSerializer.Serialize(value: expected),
                                encoding: Encoding.UTF8,
                                mediaType: "application/json")
                }));

                Uri baseAddress = new(uriString: BASE_URL_WITH_SUBPATH, uriKind: UriKind.Absolute);
                HttpClient httpClient = new(handler: handler) { BaseAddress = baseAddress };
                ContentServiceImageClient client = new(
                        httpClient: httpClient,
                        options: Microsoft.Extensions.Options.Options.Create(options: new ContentServiceClientOptions
                        {
                                BaseAddress = baseAddress
                        }));

                ContentImageMetadata actual = await client.GetImageMetadataAsync(imageId: imageId, cancellationToken: TestContext.Current.CancellationToken);

                Assert.Equal(expected: expected, actual: actual);
                Assert.Equal(
                        expected: $"https://content.oekolariet.dk/content/api/v1/images/{imageId:D}",
                        actual: handler.LastRequest?.RequestUri?.AbsoluteUri);
        }

        /// <summary>
        /// Verifies URI normalization with subpaths, null BaseAddress guard, and Guid.Empty rejection.
        /// </summary>
        [Fact]
        public void GetImageContentUri_WhenSubpathOrInvalidState_NormalizesOrThrowsExpectedException()
        {
                Guid imageId = Guid.NewGuid();
                StubHttpMessageHandler handler = new(responder: (_, _) => Task.FromResult(new HttpResponseMessage()));

                Uri subpathBase = new(uriString: BASE_URL_WITH_SUBPATH, uriKind: UriKind.Absolute);
                ContentServiceImageClient subpathClient = new(
                        httpClient: new HttpClient(handler: handler) { BaseAddress = subpathBase },
                        options: Microsoft.Extensions.Options.Options.Create(options: new ContentServiceClientOptions
                        {
                                BaseAddress = subpathBase
                        }));

                Uri uri = subpathClient.GetImageContentUri(imageId: imageId);
                Assert.Equal(
                        expected: $"https://content.oekolariet.dk/content/api/v1/images/{imageId:D}/content",
                        actual: uri.AbsoluteUri);

                Assert.Throws<ArgumentException>(testCode: () => subpathClient.GetImageContentUri(imageId: Guid.Empty));

                ContentServiceImageClient unconfiguredClient = new(
                        httpClient: new HttpClient(handler: handler),
                        options: Microsoft.Extensions.Options.Options.Create(options: new ContentServiceClientOptions()));
                Assert.Throws<InvalidOperationException>(testCode: () => unconfiguredClient.GetImageContentUri(imageId: imageId));
        }

        /// <summary>
        /// Verifies AddContentServiceSdk binds options, validates startup constraints, and resolves IContentServiceImageClient.
        /// </summary>
        [Fact]
        public void AddContentServiceSdk_WhenConfiguredOrInvalid_ResolvesClientOrFailsOptionsValidation()
        {
                IConfiguration validConfig = new ConfigurationBuilder()
                        .AddInMemoryCollection(initialData: new Dictionary<string, string?>
                        {
                                ["ContentService:BaseAddress"] = BASE_URL_WITH_SUBPATH,
                                ["ContentService:DefaultActorId"] = DEFAULT_ACTOR_ID,
                                ["ContentService:RequestTimeout"] = "00:00:30"
                        })
                        .Build();

                ServiceCollection validServices = new();
                validServices.AddContentServiceSdk(configuration: validConfig);
                using ServiceProvider validProvider = validServices.BuildServiceProvider();

                IContentServiceImageClient resolved = validProvider.GetRequiredService<IContentServiceImageClient>();
                Guid imageId = Guid.NewGuid();
                Assert.Equal(
                        expected: $"https://content.oekolariet.dk/content/api/v1/images/{imageId:D}/content",
                        actual: resolved.GetImageContentUri(imageId: imageId).AbsoluteUri);

                ServiceCollection invalidServices = new();
                invalidServices.AddContentServiceSdk(configureOptions: options =>
                {
                        options.BaseAddress = new Uri(uriString: "ftp://invalid.example.com", uriKind: UriKind.Absolute);
                });
                using ServiceProvider invalidProvider = invalidServices.BuildServiceProvider();

                Assert.Throws<OptionsValidationException>(
                        testCode: () => invalidProvider.GetRequiredService<IOptions<ContentServiceClientOptions>>().Value);
        }
}

