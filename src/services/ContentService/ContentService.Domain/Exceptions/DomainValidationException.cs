namespace ContentService.Domain.Exceptions;

/// <summary>
/// Represents a domain invariant or input validation failure detected inside entities or value objects.
/// </summary>
// Mapped by API middleware to HTTP 400 Bad Request or 422 Unprocessable Entity independently of transport validation frameworks.
public sealed class DomainValidationException : DomainException
{
        /// <summary>
        /// Initializes a new instance of the <see cref="DomainValidationException"/> class with a descriptive error message.
        /// </summary>
        /// <param name="message">The description of the failed validation rule or invariant.</param>
        public DomainValidationException(string message)
                : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DomainValidationException"/> class with a descriptive error message and inner exception.
        /// </summary>
        /// <param name="message">The description of the failed validation rule or invariant.</param>
        /// <param name="innerException">The underlying exception that triggered the validation failure.</param>
        public DomainValidationException(string message, Exception innerException)
                : base(message, innerException)
        {
        }
}
