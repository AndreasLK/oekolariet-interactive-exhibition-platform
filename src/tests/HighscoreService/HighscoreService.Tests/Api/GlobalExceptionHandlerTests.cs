using HighscoreService.Domain.Exceptions;
using HighscoreService.Hub.Api.ExceptionHandling;
using HighscoreService.Tests.TestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace HighscoreService.Tests.Api
{
        /// <summary>
        /// Unit tests for Hub and Screen <see cref="GlobalExceptionHandler"/> status code mapping.
        /// </summary>
        public sealed class GlobalExceptionHandlerTests
        {
                /// <summary>
                /// Verifies that malformed requests map to 400, missing entities map to 404, and client aborts map to 499.
                /// </summary>
                [Fact]
                public async Task HubGlobalExceptionHandler_MapsBadHttpRequestAndNotFoundAndClientAbort()
                {
                        // Wire GlobalExceptionHandler with no-op CORS doubles to isolate status-code mapping.
                        GlobalExceptionHandler handler = new(
                                NullLogger<GlobalExceptionHandler>.Instance,
                                new StubCorsService(),
                                new StubCorsPolicyProvider());

                        // Verify BadHttpRequestException produces HTTP 400.
                        DefaultHttpContext badJsonContext = new() { Response = { Body = new MemoryStream() } };
                        bool handledBad = await handler.TryHandleAsync(
                                badJsonContext,
                                new BadHttpRequestException("Malformed JSON"),
                                TestContext.Current.CancellationToken);

                        Assert.True(handledBad);
                        Assert.Equal(StatusCodes.Status400BadRequest, badJsonContext.Response.StatusCode);

                        // Verify EntityNotFoundException produces HTTP 404.
                        DefaultHttpContext notFoundContext = new() { Response = { Body = new MemoryStream() } };
                        bool handledNotFound = await handler.TryHandleAsync(
                                notFoundContext,
                                new EntityNotFoundException("Missing"),
                                TestContext.Current.CancellationToken);

                        Assert.True(handledNotFound);
                        Assert.Equal(StatusCodes.Status404NotFound, notFoundContext.Response.StatusCode);

                        // Verify client-aborted OperationCanceledException maps to non-standard HTTP 499.
                        using CancellationTokenSource abortedCts = new();
                        await abortedCts.CancelAsync();
                        DefaultHttpContext abortedContext = new()
                        {
                                RequestAborted = abortedCts.Token,
                                Response = { Body = new MemoryStream() }
                        };

                        bool handledAbort = await handler.TryHandleAsync(
                                abortedContext,
                                new OperationCanceledException(abortedCts.Token),
                                TestContext.Current.CancellationToken);

                        Assert.True(handledAbort);
                        Assert.Equal(499, abortedContext.Response.StatusCode);
                }
        }
}

