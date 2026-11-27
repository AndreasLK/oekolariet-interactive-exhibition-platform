using HighscoreService.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace HighscoreService.Screen.Api.ExceptionHandling
{
        /// <summary>
        /// Converts domain and unhandled exceptions into RFC 7807 <see cref="ProblemDetails"/> responses.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
        {
                private const int CLIENT_CLOSED_REQUEST_STATUS_CODE = 499;

                private readonly ILogger<GlobalExceptionHandler> _logger = logger;

                /// <inheritdoc />
                public async ValueTask<bool> TryHandleAsync(
                        HttpContext httpContext,
                        Exception exception,
                        CancellationToken cancellationToken)
                {
                        // WHY: Treat caller-aborted requests as 499 Client Closed Request instead of
                        // logging false-positive 500 errors when a local game client cancels mid-flight.
                        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
                        {
                                httpContext.Response.StatusCode = CLIENT_CLOSED_REQUEST_STATUS_CODE;
                                return true;
                        }

                        (int statusCode, string title) = exception switch
                        {
                                DomainValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
                                BadHttpRequestException => (StatusCodes.Status400BadRequest, "Malformed request"),
                                EntityNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
                                _ => (StatusCodes.Status500InternalServerError, "Internal server error")
                        };

                        if (statusCode == StatusCodes.Status500InternalServerError)
                        {
                                this._logger.LogError(exception, "Unhandled exception while processing request.");
                        }

                        ProblemDetails details = new()
                        {
                                Status = statusCode,
                                Title = title,
                                Detail = statusCode == StatusCodes.Status500InternalServerError
                                        ? "An unexpected error occurred."
                                        : exception.Message,
                                Instance = httpContext.Request.Path
                        };

                        httpContext.Response.StatusCode = statusCode;
                        await httpContext.Response.WriteAsJsonAsync(details, cancellationToken);
                        return true;
                }
        }
}

