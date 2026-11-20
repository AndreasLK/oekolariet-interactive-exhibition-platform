namespace ContentService.Domain.Exceptions;

// Thrown when attempting to delete or modify an entity (such as an image) that is still referenced by active templates, preserving referential integrity.
public sealed class EntityInUseException : DomainException
{
        public EntityInUseException(string message)
                : base(message: message)
        {
        }

        public EntityInUseException(string message, Exception innerException)
                : base(message: message, innerException: innerException)
        {
        }
}
