namespace HighscoreService.Domain.Exceptions
{
        /// <summary>
        /// Base exception for domain rule violations in the Highscore Service.
        /// </summary>
        /// <param name="message">The human-readable error description.</param>
        public abstract class DomainException(string message) : Exception(message)
        {
        }
}

