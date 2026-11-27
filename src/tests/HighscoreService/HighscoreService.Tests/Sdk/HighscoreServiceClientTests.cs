using System.Net;
using System.Net.Http.Json;
using HighscoreService.Sdk.Client;
using HighscoreService.Sdk.Exceptions;
using HighscoreService.Sdk.Models.Highscores.Requests;
using HighscoreService.Sdk.Models.Highscores.Responses;
using HighscoreService.Tests.TestDoubles;

namespace HighscoreService.Tests.Sdk
{
        /// <summary>
        /// Unit tests for <see cref="HighscoreServiceClient"/> failover, cancellation, and Hub-only operations.
        /// </summary>
        public sealed class HighscoreServiceClientTests
        {
                /// <summary>
                /// Ensures <see cref="HighscoreServiceClient.RecordAsync"/> fails over to Screen when the Hub times out.
                /// </summary>
                [Fact]
                public async Task RecordAsync_FallsBackToScreen_WhenHubTimesOut()
                {
                        // Arrange: Hub blocks until its per-call timeout token fires; Screen returns 201 Created.
                        using HttpClient hubClient = new(new StubHttpMessageHandler(async (_, ct) =>
                        {
                                await Task.Delay(TimeSpan.FromSeconds(10), ct);
                                return new HttpResponseMessage(HttpStatusCode.OK);
                        }))
                        {
                                BaseAddress = new Uri("https://hub.oekolariet.dk/highscore")
                        };

                        bool hitScreen = false;
                        using HttpClient screenClient = new(new StubHttpMessageHandler((req, _) =>
                        {
                                hitScreen = true;
                                HighscoreResponse body = new(Guid.NewGuid(), "Ada", 12, 500, "AA:BB:CC:DD:EE:01", Guid.NewGuid(), DateTimeOffset.UtcNow, 1, false);
                                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(body) });
                        }))
                        {
                                BaseAddress = new Uri("http://localhost:5281")
                        };

                        HighscoreServiceClient sut = new(hubClient, screenClient, hubTimeoutSeconds: 1);

                        // Act
                        HighscoreResponse result = await sut.RecordAsync(
                                new RecordHighscoreRequest(null, "Ada", 12, 500, "AA:BB:CC:DD:EE:01", Guid.NewGuid(), null),
                                TestContext.Current.CancellationToken);

                        // Assert
                        Assert.True(hitScreen);
                        Assert.Equal("Ada", result.Name);
                }

                /// <summary>
                /// Ensures caller-initiated cancellation does not trigger Screen fallback.
                /// </summary>
                [Fact]
                public async Task RecordAsync_DoesNotFallBack_WhenCallerCancels()
                {
                        // Arrange
                        using CancellationTokenSource callerCts = new();
                        await callerCts.CancelAsync();

                        using HttpClient hubClient = new(new StubHttpMessageHandler((_, ct) =>
                                Task.FromCanceled<HttpResponseMessage>(ct)))
                        {
                                BaseAddress = new Uri("https://hub.oekolariet.dk")
                        };

                        bool hitScreen = false;
                        using HttpClient screenClient = new(new StubHttpMessageHandler((_, _) =>
                        {
                                hitScreen = true;
                                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created));
                        }))
                        {
                                BaseAddress = new Uri("http://localhost:5281")
                        };

                        HighscoreServiceClient sut = new(hubClient, screenClient, hubTimeoutSeconds: 2);

                        // Act & Assert
                        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                                sut.RecordAsync(new RecordHighscoreRequest(null, "Ada", 12, 500, "AA:BB:CC:DD:EE:01", Guid.NewGuid(), null), callerCts.Token));
                        Assert.False(hitScreen);
                }

                /// <summary>
                /// Ensures 4xx validation errors from the Hub are rethrown without failing over to Screen.
                /// </summary>
                [Fact]
                public async Task RecordAsync_DoesNotFallBack_WhenHubReturnsBadRequest()
                {
                        // Arrange
                        using HttpClient hubClient = new(new StubHttpMessageHandler((_, _) =>
                                Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
                                {
                                        Content = JsonContent.Create(new HighscoreProblemDetails(null, "Validation failed", 400, "Invalid score.", null))
                                })))
                        {
                                BaseAddress = new Uri("https://hub.oekolariet.dk")
                        };

                        bool hitScreen = false;
                        using HttpClient screenClient = new(new StubHttpMessageHandler((_, _) =>
                        {
                                hitScreen = true;
                                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created));
                        }))
                        {
                                BaseAddress = new Uri("http://localhost:5281")
                        };

                        HighscoreServiceClient sut = new(hubClient, screenClient, hubTimeoutSeconds: 2);

                        // Act & Assert
                        HighscoreServiceApiException ex = await Assert.ThrowsAsync<HighscoreServiceApiException>(() =>
                                sut.RecordAsync(new RecordHighscoreRequest(null, "Ada", 12, -1, "AA:BB:CC:DD:EE:01", Guid.NewGuid(), null), TestContext.Current.CancellationToken));
                        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
                        Assert.False(hitScreen);
                }

                /// <summary>
                /// Ensures Hub-only operations (such as statistics) never fall back to Screen when Hub fails.
                /// </summary>
                [Fact]
                public async Task GetStatisticsAsync_CallsHubOnly_EvenWhenHubFails()
                {
                        // Arrange
                        using HttpClient hubClient = new(new StubHttpMessageHandler((_, _) =>
                                Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))))
                        {
                                BaseAddress = new Uri("https://hub.oekolariet.dk")
                        };

                        bool hitScreen = false;
                        using HttpClient screenClient = new(new StubHttpMessageHandler((_, _) =>
                        {
                                hitScreen = true;
                                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                        }))
                        {
                                BaseAddress = new Uri("http://localhost:5281")
                        };

                        HighscoreServiceClient sut = new(hubClient, screenClient, hubTimeoutSeconds: 2);

                        // Act & Assert
                        await Assert.ThrowsAsync<HighscoreServiceApiException>(() =>
                                sut.GetStatisticsAsync(new HighscoreQueryRequest(), TestContext.Current.CancellationToken));
                        Assert.False(hitScreen);
                }

                /// <summary>
                /// Ensures <see cref="HighscoreServiceClient.QueryAsync"/> fails over to Screen on Hub 503.
                /// </summary>
                [Fact]
                public async Task QueryAsync_FallsBackToScreen_WhenHubReturns503()
                {
                        // Arrange
                        using HttpClient hubClient = new(new StubHttpMessageHandler((_, _) =>
                                Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))))
                        {
                                BaseAddress = new Uri("https://hub.oekolariet.dk")
                        };

                        bool hitScreen = false;
                        using HttpClient screenClient = new(new StubHttpMessageHandler((_, _) =>
                        {
                                hitScreen = true;
                                PagedHighscoreResponse page = new([], 0, 1, 25);
                                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(page) });
                        }))
                        {
                                BaseAddress = new Uri("http://localhost:5281")
                        };

                        HighscoreServiceClient sut = new(hubClient, screenClient, hubTimeoutSeconds: 2);

                        // Act
                        PagedHighscoreResponse result = await sut.QueryAsync(new HighscoreQueryRequest(), TestContext.Current.CancellationToken);

                        // Assert
                        Assert.True(hitScreen);
                        Assert.Empty(result.Items);
                }

                /// <summary>
                /// Ensures <see cref="HighscoreServiceClient.DeleteByIdAsync"/> and <see cref="HighscoreServiceClient.DeleteByGameIdAsync"/>
                /// never fall back to Screen when Hub fails.
                /// </summary>
                [Fact]
                public async Task DeleteOperations_CallHubOnly_EvenWhenHubFails()
                {
                        // Arrange
                        using HttpClient hubClient = new(new StubHttpMessageHandler((_, _) =>
                                Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))))
                        {
                                BaseAddress = new Uri("https://hub.oekolariet.dk")
                        };

                        bool hitScreen = false;
                        using HttpClient screenClient = new(new StubHttpMessageHandler((_, _) =>
                        {
                                hitScreen = true;
                                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
                        }))
                        {
                                BaseAddress = new Uri("http://localhost:5281")
                        };

                        HighscoreServiceClient sut = new(hubClient, screenClient, hubTimeoutSeconds: 2);

                        // Act & Assert
                        await Assert.ThrowsAsync<HighscoreServiceApiException>(() =>
                                sut.DeleteByIdAsync(Guid.CreateVersion7(), TestContext.Current.CancellationToken));
                        await Assert.ThrowsAsync<HighscoreServiceApiException>(() =>
                                sut.DeleteByGameIdAsync(Guid.CreateVersion7(), TestContext.Current.CancellationToken));
                        Assert.False(hitScreen);
                }

                /// <summary>
                /// Ensures non-JSON error bodies from the API still surface the HTTP status code and reason phrase cleanly.
                /// </summary>
                [Fact]
                public async Task RecordAsync_SurfacesReasonPhrase_WhenErrorBodyIsNotJson()
                {
                        // Arrange
                        using HttpClient hubClient = new(new StubHttpMessageHandler((_, _) =>
                                Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)
                                {
                                        ReasonPhrase = "Bad Gateway",
                                        Content = new StringContent("<html>502</html>")
                                })))
                        {
                                BaseAddress = new Uri("https://hub.oekolariet.dk")
                        };

                        HighscoreServiceClient sut = new(hubClient);

                        // Act & Assert
                        HighscoreServiceApiException ex = await Assert.ThrowsAsync<HighscoreServiceApiException>(() =>
                                sut.RecordAsync(
                                        new RecordHighscoreRequest(null, "Ada", 12, 100, "AA:BB:CC:DD:EE:01", Guid.NewGuid(), null),
                                        TestContext.Current.CancellationToken));

                        Assert.Equal(HttpStatusCode.BadGateway, ex.StatusCode);
                        Assert.Null(ex.ProblemDetails);
                        Assert.Contains("Bad Gateway", ex.Message);
                }
        }
}
