namespace ContentService.Domain.Exceptions;

// Thrown when an entity or value object invariant is violated (e.g., invalid file name, oversized payload, or malformed reference), even if UI/API validation already ran.
public sealed class DomainValidationException : DomainException
{
        public DomainValidationException(string message)
                : base(message: message)
        {
        }

        public DomainValidationException(string message, Exception innerException)
                : base(message: message, innerException: innerException)
        {
        }
}
