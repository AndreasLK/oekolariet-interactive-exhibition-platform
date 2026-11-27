using HighscoreService.Domain.Entities;

namespace HighscoreService.Application.Models
{
        /// <summary>
        /// Outcome of an idempotent highscore insertion attempt.
        /// </summary>
        public sealed record HighscoreInsertOutcome(
                HighscoreEntity Entity,
                bool WasInserted);
}

