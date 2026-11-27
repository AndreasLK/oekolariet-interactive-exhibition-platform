namespace HighscoreService.Domain.Exceptions
{
        /// <summary>
        /// Thrown when a requested highscore record cannot be found.
        /// </summary>
        /// <param name="message">The not-found error message.</param>
        public sealed class EntityNotFoundException(string message) : DomainException(message)
        {
        }
}

