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
                                _ => (0, string.Empty)
                        };

                        // Unknown exception: let ASP.NET Core return 500 without details (OWASP).
                        if (statusCode == 0)
                        {
                                return false;
                        }

                        httpContext.Response.StatusCode = statusCode;

                        return await this._problemDetails.TryWriteAsync(context: new ProblemDetailsContext
                        {
                                HttpContext = httpContext,
                                Exception = exception,
                                ProblemDetails = new ProblemDetails
                                {
                                        Title = title,
                                        Detail = exception.Message,
                                        Status = statusCode
                                }
                        });
                }
        }
}
