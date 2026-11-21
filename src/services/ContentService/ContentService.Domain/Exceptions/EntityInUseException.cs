namespace ContentService.Domain.Exceptions;

/// <summary>
/// Represents a referential integrity conflict when an entity cannot be deleted or modified because active dependents still reference it.
/// </summary>
// Mapped by API middleware to HTTP 409 Conflict without coupling the domain to SQL foreign-key error numbers.
public sealed class EntityInUseException : DomainException
{
        /// <summary>
        /// Initializes a new instance of the <see cref="EntityInUseException"/> class with a descriptive error message.
        /// </summary>
        /// <param name="message">The description of the entity and blocking references.</param>
        public EntityInUseException(string message)
                : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EntityInUseException"/> class with a descriptive error message and inner exception.
        /// </summary>
        /// <param name="message">The description of the entity and blocking references.</param>
        /// <param name="innerException">The underlying exception associated with the dependency check.</param>
        public EntityInUseException(string message, Exception innerException)
                : base(message, innerException)
        {
        }
}
