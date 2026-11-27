namespace HighscoreService.Sdk.Models.Statistics.Responses
{
        /// <summary>
        /// Per-game metrics returned in statistics reports.
        /// </summary>
        public sealed record GameStatisticsResponse(
                Guid GameId,
                int TotalPlays,
                int CompletedPlays,
                int TimedOutPlays,
                int UnnamedPlays,
                double AbandonmentRatePercent,
                double AverageScore,
                double MedianScore,
                int MaxScore,
                RankDistributionResponse RankDistribution);
}

