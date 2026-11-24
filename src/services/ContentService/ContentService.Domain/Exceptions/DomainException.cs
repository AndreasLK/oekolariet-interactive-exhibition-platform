namespace ContentService.Domain.Exceptions;

/// <summary>
/// Serves as the abstract base class for all expected business-rule and invariant failures in ContentService.Domain.
/// </summary>
// Decouples domain failures from EF Core and ASP.NET Core so API middleware can map subclasses deterministically to HTTP status codes (e.g., 400/422 vs 409) without leaking infrastructure types.
public abstract class DomainException : Exception
{
        /// <summary>
        /// Initializes a new instance of the <see cref="DomainException"/> class with a descriptive error message.
        /// </summary>
        /// <param name="message">The human-readable description of the domain rule violation.</param>
        // Parameterless constructors are omitted so every domain failure carries a clear message for logs and API responses.
        protected DomainException(string message)
                : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DomainException"/> class with a descriptive error message and inner exception.
        /// </summary>
        /// <param name="message">The human-readable description of the domain rule violation.</param>
        /// <param name="innerException">The underlying exception that caused this domain failure.</param>
        protected DomainException(string message, Exception innerException)
                : base(message, innerException)
        {
        }
}
