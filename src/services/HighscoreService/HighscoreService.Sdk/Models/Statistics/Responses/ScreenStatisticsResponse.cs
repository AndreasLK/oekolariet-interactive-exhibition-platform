namespace HighscoreService.Sdk.Models.Statistics.Responses
{
        /// <summary>
        /// Per-screen metrics returned in statistics reports.
        /// </summary>
        public sealed record ScreenStatisticsResponse(
                string SenderMacAddress,
                int TotalPlays,
                int CompletedPlays,
                int TimedOutPlays,
                double AbandonmentRatePercent,
                double AverageScore,
                DateTimeOffset? LastPlayedAtUtc);
}

