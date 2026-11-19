namespace ContentService.Domain.Exceptions;

/// <summary>
/// Represents an error thrown when domain invariants or input validation rules are violated.
/// </summary>
public sealed class DomainValidationException : DomainException
{
        /// <summary>
        /// Initializes a new instance of the <see cref="DomainValidationException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the validation error.</param>
        public DomainValidationException(string message) : base(message: message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DomainValidationException"/> class with a specified error message and inner exception.
        /// </summary>
        /// <param name="message">The message that describes the validation error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public DomainValidationException(string message, Exception innerException) : base(message: message, innerException: innerException)
        {
        }
}
