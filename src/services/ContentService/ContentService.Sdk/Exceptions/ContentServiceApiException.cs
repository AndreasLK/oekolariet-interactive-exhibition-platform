using System.Net;

namespace ContentService.Sdk.Exceptions;

/// <summary>
/// Represents an HTTP error returned by ContentService, preserving status code and optional RFC 7807 problem details.
/// </summary>
public sealed class ContentServiceApiException : Exception
{
        /// <summary>
        /// Initializes a new instance of <see cref="ContentServiceApiException"/>.
        /// </summary>
        /// <param name="statusCode">The HTTP status code returned by ContentService or an intermediary.</param>
        /// <param name="message">A diagnostic message summarizing the failure.</param>
        /// <param name="problemDetails">The parsed RFC 7807 payload when the response contained valid problem JSON.</param>
        /// <param name="rawResponseBody">A bounded raw text snippet when the response body was non-JSON or failed deserialization.</param>
        /// <param name="innerException">An optional inner exception that caused this error.</param>
        public ContentServiceApiException(
                HttpStatusCode statusCode,
                string message,
                ContentServiceProblemDetails? problemDetails = null,
                string? rawResponseBody = null,
                Exception? innerException = null)
                : base(message: message, innerException: innerException)
        {
                this.StatusCode = statusCode;
                this.ProblemDetails = problemDetails;
                this.RawResponseBody = rawResponseBody;
        }

        /// <summary>
        /// Gets the HTTP status code associated with the failed request.
        /// </summary>
        public HttpStatusCode StatusCode { get; }

        /// <summary>
        /// Gets the parsed RFC 7807 problem details when available, or <see langword="null"/> for non-JSON responses.
        /// </summary>
        public ContentServiceProblemDetails? ProblemDetails { get; }

        /// <summary>
        /// Gets a bounded raw response snippet when the body was not valid ProblemDetails JSON, or <see langword="null"/> otherwise.
        /// </summary>
        public string? RawResponseBody { get; }
}

