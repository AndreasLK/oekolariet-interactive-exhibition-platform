using EditorService.Application.Exceptions;
using EditorService.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace EditorService.Api.ExceptionHandling
{
        /// <summary>
        /// Turns known exceptions into HTTP status codes with a ProblemDetails body.
        /// Unknown exceptions are left to ASP.NET Core, which returns a generic 500.
        /// </summary>
        public sealed class GlobalExceptionHandler : IExceptionHandler
        {
                private readonly IProblemDetailsService _problemDetails;

                /// <summary>
                /// Initializes a new instance of the <see cref="GlobalExceptionHandler"/> class.
                /// </summary>
                /// <param name="problemDetails">Writes the ProblemDetails response.</param>
                public GlobalExceptionHandler(IProblemDetailsService problemDetails)
                {
                        this._problemDetails = problemDetails;
                }

                /// <summary>
                /// Maps the exception to a status code and writes the response.
                /// </summary>
                /// <param name="httpContext">The current request.</param>
                /// <param name="exception">The exception that was thrown.</param>
                /// <param name="cancellationToken">Stops the call if the request is cancelled.</param>
                /// <returns><see langword="true"/> if the exception was handled; otherwise <see langword="false"/>.</returns>
                public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
                {
                        // Specific exceptions first. DomainException last, because all the others are also DomainExceptions.
                        (int statusCode, string title) = exception switch
                        {
                                GameLockedException => (StatusCodes.Status423Locked, "Spillet er låst"),
                                EditLockRequiredException => (StatusCodes.Status409Conflict, "Du har ikke låsen"),
                                ConcurrencyConflictException => (StatusCodes.Status409Conflict, "Spillet er ændret af en anden"),
                                DomainValidationException => (StatusCodes.Status400BadRequest, "Valideringsfejl"),
                                DomainException => (StatusCodes.Status400BadRequest, "Forretningsregel overtrådt"),
                                GameNotFoundException => (StatusCodes.Status404NotFound, "Spillet findes ikke"),
                                _ => (0, string.Empty) // Any other exception: mark it as unknown with status code 0.
                        };

                        // Unknown exceptions are not handled here. ASP.NET Core returns a generic 500
                        // without details, so internal information is never shown to the user (OWASP).
                        if (statusCode == 0)
                        {
                                return false;
                        }

                        // Set the HTTP status code on the response, e.g. 423 when the game is locked.
                        httpContext.Response.StatusCode = statusCode;

                        // Write the error as ProblemDetails JSON, the standard format for API errors (RFC 9457).
                        // Returns true when the response is written, so ASP.NET Core knows the error is handled.
                        return await this._problemDetails.TryWriteAsync(context: new ProblemDetailsContext
                        {
                                // The current request and the exception that was thrown.
                                HttpContext = httpContext,
                                Exception = exception,

                                // The content the client receives.
                                ProblemDetails = new ProblemDetails
                                {
                                        // Short heading from the switch above, e.g. "Spillet er låst".
                                        Title = title,

                                        // The message from our own exception, e.g. who holds the lock.
                                        // Safe to show, because only our own domain exceptions reach this point.
                                        Detail = exception.Message,

                                        // The same status code, so the client can also read it from the JSON.
                                        Status = statusCode
                                }
                        });
                }
        }
}
