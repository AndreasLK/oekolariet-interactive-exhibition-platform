namespace ContentService.Domain.Exceptions;

/// <summary>
/// Represents an optimistic concurrency failure when persisting stale entity changes.
/// </summary>
// Mapped by API middleware to HTTP 409 Conflict instead of leaking raw EF Core DbUpdateConcurrencyException across layer boundaries.
public sealed class ConcurrencyConflictException : DomainException
{
        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrencyConflictException"/> class with a descriptive error message.
        /// </summary>
        /// <param name="message">The description of the conflicting concurrent update.</param>
        public ConcurrencyConflictException(string message)
                : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrencyConflictException"/> class with a descriptive error message and inner exception.
        /// </summary>
        /// <param name="message">The description of the conflicting concurrent update.</param>
        /// <param name="innerException">The underlying concurrency exception caught at the persistence boundary.</param>
        public ConcurrencyConflictException(string message, Exception innerException)
                : base(message, innerException)
        {
        }
}
