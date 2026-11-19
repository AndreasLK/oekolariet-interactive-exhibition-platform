namespace ContentService.Domain.Exceptions;

/// <summary>
/// Serves as the base class for all domain-specific exceptions in the Content Service.
/// </summary>
public abstract class DomainException : Exception
{
        /// <summary>
        /// Initializes a new instance of the <see cref="DomainException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        protected DomainException(string message) : base(message: message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DomainException"/> class with a specified error message and inner exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        protected DomainException(string message, Exception innerException) : base(message: message, innerException: innerException)
        {
        }
}
