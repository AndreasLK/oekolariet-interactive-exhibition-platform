namespace ContentService.Domain.Exceptions;

// Abstract base for all expected business-rule failures so upper layers (e.g., API middleware) can distinguish domain errors from unexpected crashes.
public abstract class DomainException : Exception
{
        // Protected constructors ensure only specific domain exception subclasses can be instantiated.
        protected DomainException(string message)
                : base(message: message)
        {
        }

        protected DomainException(string message, Exception innerException)
                : base(message: message, innerException: innerException)
        {
        }
}
