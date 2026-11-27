namespace HighscoreService.Application.Models
{
        /// <summary>
        /// Comprehensive exhibition analytics report covering scores, abandonment, age brackets, time trends, games, and screens.
        /// </summary>
        public sealed record HighscoreStatisticsReport(
                DateTimeOffset GeneratedAtUtc,
                int TotalPlays,
                int CompletedPlays,
                int TimedOutPlays,
                int UnnamedPlays,
                int AbandonedPlays,
                double CompletionRatePercent,
                double AbandonmentRatePercent,
                double AverageScore,
                double MedianScore,
                int MinScore,
                int MaxScore,
                int Percentile90Score,
                RankDistributionStatistics RankDistribution,
                IReadOnlyList<AgeBracketStatistics> AgeBrackets,
                IReadOnlyList<TimeBucketStatistics> HourlyTrend,
                IReadOnlyList<GameStatistics> PerGame,
                IReadOnlyList<ScreenStatistics> PerScreen);
}

