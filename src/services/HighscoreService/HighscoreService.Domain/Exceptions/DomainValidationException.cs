namespace HighscoreService.Domain.Exceptions
{
        /// <summary>
        /// Thrown when input data or entity state violates a domain invariant.
        /// </summary>
        /// <param name="message">The validation failure message.</param>
        public sealed class DomainValidationException(string message) : DomainException(message)
        {
        }
}

