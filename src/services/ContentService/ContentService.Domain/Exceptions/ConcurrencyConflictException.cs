namespace ContentService.Domain.Exceptions;

// Thrown when two editors or requests load the same entity version and try to save conflicting changes; the second write fails instead of silently overwriting.
public sealed class ConcurrencyConflictException : DomainException
{
        public ConcurrencyConflictException(string message)
                : base(message: message)
        {
        }

        public ConcurrencyConflictException(string message, Exception innerException)
                : base(message: message, innerException: innerException)
        {
        }
}
