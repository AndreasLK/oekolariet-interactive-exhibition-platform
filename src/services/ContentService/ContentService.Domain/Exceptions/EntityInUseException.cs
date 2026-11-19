namespace ContentService.Domain.Exceptions;

/// <summary>
/// Represents an error thrown when an operation cannot proceed because the target entity is currently in use.
/// </summary>
public sealed class EntityInUseException : DomainException
{
        /// <summary>
        /// Initializes a new instance of the <see cref="EntityInUseException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the conflict.</param>
        public EntityInUseException(string message) : base(message: message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EntityInUseException"/> class with a specified error message and inner exception.
        /// </summary>
        /// <param name="message">The message that describes the conflict.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public EntityInUseException(string message, Exception innerException) : base(message: message, innerException: innerException)
        {
        }
}
