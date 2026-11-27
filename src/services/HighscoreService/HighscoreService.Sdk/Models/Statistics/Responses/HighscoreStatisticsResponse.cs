namespace HighscoreService.Sdk.Models.Statistics.Responses
{
        /// <summary>
        /// Comprehensive exhibition statistics report returned by the Highscore Service API.
        /// </summary>
        public sealed record HighscoreStatisticsResponse(
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
                RankDistributionResponse RankDistribution,
                IReadOnlyList<AgeBracketStatisticsResponse> AgeBrackets,
                IReadOnlyList<TimeBucketStatisticsResponse> HourlyTrend,
                IReadOnlyList<GameStatisticsResponse> PerGame,
                IReadOnlyList<ScreenStatisticsResponse> PerScreen);
}

