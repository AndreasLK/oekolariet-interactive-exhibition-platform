using System.Net;
using System.Net.Http.Json;
using HighscoreService.Application.Interfaces.CommandHandlers;
using HighscoreService.Facade.Commands;
using HighscoreService.Hub.Api.Authentication;
using HighscoreService.Hub.Api.Endpoints.Highscores;
using HighscoreService.Hub.Api.ExceptionHandling;
using HighscoreService.Hub.Api.Extensions;
using HighscoreService.Sdk.Client;
using HighscoreService.Sdk.Models.Highscores.Requests;
using HighscoreService.Tests.TestDoubles;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HighscoreService.Tests.Api
{
        /// <summary>
        /// Verifies reverse-proxy header forwarding, dual LAN/external path handling, CORS on error responses,
        /// and SDK subpath URI resolution for Hub.Api.
        /// </summary>
        public sealed class HubReverseProxyTests : IAsyncLifetime
        {
                private WebApplication? _app;

                /// <inheritdoc />
                public async ValueTask InitializeAsync()
                {
                        // Configure an in-memory TestServer mimicking YARP/nginx reverse-proxy headers and PathBase.
                        WebApplicationBuilder builder = WebApplication.CreateBuilder();
                        builder.WebHost.UseTestServer();

                        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                                ["PathBase"] = "/highscore",
                                ["Cors:AllowedOrigins:0"] = "https://oekolariet.dk",
                                ["Cors:AllowCredentials"] = "false"
                        });

                        builder.Services.AddProblemDetails();
                        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
                        builder.Services.AddHubReverseProxyAndCors(builder.Configuration);
                        builder.Services.AddAuthorizationBuilder()
                                .AddPolicy(HubAuthSetup.POLICY_SCREEN_OR_USER, policy => policy.RequireAssertion(_ => true));
                        builder.Services.AddSingleton<IRecordHighscoreCommandHandler, StubRecordHighscoreCommandHandler>();

                        this._app = builder.Build();
                        this._app.UseHubReverseProxyAndCors(builder.Configuration);
                        this._app.UseAuthorization();
                        this._app.MapRecordHighscore();

                        await this._app.StartAsync();
                }

                /// <inheritdoc />
                public async ValueTask DisposeAsync()
                {
                        if (this._app is not null)
                        {
                                await this._app.DisposeAsync();
                        }
                }

                /// <summary>
                /// Ensures <c>POST /api/v1/highscores</c> returns a proxy-prefixed <c>Location</c> header when
                /// <c>X-Forwarded-Prefix</c> is present, and a root-relative <c>Location</c> for direct LAN calls.
                /// </summary>
                [Fact]
                public async Task HubApi_RespectsForwardedPrefixAndDirectLanPaths()
                {
                        // Exercise both external reverse-proxied requests and direct LAN requests without PathBase.
                        CancellationToken ct = TestContext.Current.CancellationToken;
                        using HttpClient client = this._app!.GetTestClient();

                        RecordHighscoreCommand payload = new(
                                Id: Guid.NewGuid(),
                                Name: "ProxyTester",
                                Age: 12,
                                Score: 300,
                                SenderMacAddress: "AA:BB:CC:DD:EE:10",
                                GameId: Guid.NewGuid(),
                                PlayedAtUtc: DateTimeOffset.UtcNow,
                                IsTimedOut: false);

                        using HttpRequestMessage proxiedRequest = new(HttpMethod.Post, "/api/v1/highscores")
                        {
                                Content = JsonContent.Create(payload)
                        };
                        proxiedRequest.Headers.Add("X-Forwarded-Proto", "https");
                        proxiedRequest.Headers.Add("X-Forwarded-Host", "oekolariet.dk");
                        proxiedRequest.Headers.Add("X-Forwarded-Prefix", "/highscore");

                        using HttpResponseMessage proxiedResponse = await client.SendAsync(proxiedRequest, ct);
                        Assert.Equal(HttpStatusCode.Created, proxiedResponse.StatusCode);
                        Assert.Equal($"/highscore/api/v1/highscores/{payload.Id}", proxiedResponse.Headers.Location?.ToString());

                        RecordHighscoreCommand lanPayload = payload with { Id = Guid.NewGuid() };
                        using HttpResponseMessage lanResponse = await client.PostAsJsonAsync("/api/v1/highscores", lanPayload, ct);
                        Assert.Equal(HttpStatusCode.Created, lanResponse.StatusCode);
                        Assert.Equal($"/api/v1/highscores/{lanPayload.Id}", lanResponse.Headers.Location?.ToString());
                }

                /// <summary>
                /// Ensures CORS headers are preserved even when <c>GlobalExceptionHandler</c> handles a validation error.
                /// </summary>
                [Fact]
                public async Task HubApi_PreservesCorsHeadersOnProblemDetailsErrors()
                {
                        CancellationToken ct = TestContext.Current.CancellationToken;
                        using HttpClient client = this._app!.GetTestClient();

                        RecordHighscoreCommand invalidPayload = new(
                                Id: Guid.NewGuid(),
                                Name: "",
                                Age: 12,
                                Score: 100,
                                SenderMacAddress: "AA:BB:CC:DD:EE:11",
                                GameId: Guid.NewGuid(),
                                PlayedAtUtc: DateTimeOffset.UtcNow,
                                IsTimedOut: false);

                        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/highscores")
                        {
                                Content = JsonContent.Create(invalidPayload)
                        };
                        request.Headers.Add("Origin", "https://oekolariet.dk");

                        using HttpResponseMessage response = await client.SendAsync(request, ct);
                        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out IEnumerable<string>? origins));
                        Assert.Contains("https://oekolariet.dk", origins);
                }

                /// <summary>
                /// Ensures <see cref="HighscoreServiceClient"/> preserves a reverse-proxy subpath when
                /// <see cref="HttpClient.BaseAddress"/> omits a trailing slash.
                /// </summary>
                [Fact]
                public async Task SdkClient_PreservesSubpathWhenBaseAddressLacksTrailingSlash()
                {
                        CancellationToken ct = TestContext.Current.CancellationToken;
                        Uri? capturedUri = null;
                        using HttpClient httpClient = new(new CapturingHandler(uri => capturedUri = uri))
                        {
                                BaseAddress = new Uri("https://oekolariet.dk/highscore")
                        };

                        HighscoreServiceClient sdkClient = new(httpClient);
                        await sdkClient.QueryAsync(new HighscoreQueryRequest(GameId: Guid.NewGuid()), ct);

                        Assert.Equal("https://oekolariet.dk/highscore/api/v1/highscores/query", capturedUri?.AbsoluteUri);
                }
        }
}
