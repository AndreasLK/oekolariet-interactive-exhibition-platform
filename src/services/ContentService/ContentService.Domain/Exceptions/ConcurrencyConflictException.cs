namespace ContentService.Domain.Exceptions;

/// <summary>
/// Represents an error thrown when an optimistic concurrency conflict is detected.
/// </summary>
public sealed class ConcurrencyConflictException : DomainException
{
        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrencyConflictException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the concurrency conflict.</param>
        public ConcurrencyConflictException(string message) : base(message: message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrencyConflictException"/> class with a specified error message and inner exception.
        /// </summary>
        /// <param name="message">The message that describes the concurrency conflict.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public ConcurrencyConflictException(string message, Exception innerException) : base(message: message, innerException: innerException)
        {
        }
}
