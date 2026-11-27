using System.Net;

namespace HighscoreService.Sdk.Exceptions
{
        /// <summary>
        /// Exception thrown when the Highscore Service HTTP API returns a non-success status code.
        /// </summary>
        /// <param name="statusCode">The HTTP status code returned by the API.</param>
        /// <param name="problemDetails">Optional RFC 7807 problem details payload.</param>
        /// <param name="message">The exception message.</param>
        public sealed class HighscoreServiceApiException(
                HttpStatusCode statusCode,
                HighscoreProblemDetails? problemDetails,
                string message) : Exception(message)
        {
                /// <summary>
                /// Gets the HTTP status code returned by the API.
                /// </summary>
                public HttpStatusCode StatusCode { get; } = statusCode;

                /// <summary>
                /// Gets the parsed RFC 7807 problem details, if available.
                /// </summary>
                public HighscoreProblemDetails? ProblemDetails { get; } = problemDetails;
        }
}

