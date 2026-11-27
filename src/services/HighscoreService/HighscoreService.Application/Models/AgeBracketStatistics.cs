namespace HighscoreService.Application.Models
{
        /// <summary>
        /// Aggregated play, score, and abandonment metrics for a single player age bracket.
        /// </summary>
        public sealed record AgeBracketStatistics(
                string BracketLabel,
                int? MinAge,
                int? MaxAge,
                int TotalPlays,
                int CompletedPlays,
                int AbandonedPlays,
                double AbandonmentRatePercent,
                double AverageScore,
                int MaxScore);
}

