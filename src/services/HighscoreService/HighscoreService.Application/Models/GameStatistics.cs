namespace HighscoreService.Application.Models
{
        /// <summary>
        /// Aggregated play, score, and abandonment metrics for a specific game.
        /// </summary>
        public sealed record GameStatistics(
                Guid GameId,
                int TotalPlays,
                int CompletedPlays,
                int TimedOutPlays,
                int UnnamedPlays,
                double AbandonmentRatePercent,
                double AverageScore,
                double MedianScore,
                int MaxScore,
                RankDistributionStatistics RankDistribution);
}

