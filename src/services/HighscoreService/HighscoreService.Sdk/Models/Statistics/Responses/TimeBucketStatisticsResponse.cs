namespace HighscoreService.Sdk.Models.Statistics.Responses
{
        /// <summary>
        /// Hourly trend metrics returned in statistics reports.
        /// </summary>
        public sealed record TimeBucketStatisticsResponse(
                DateTimeOffset BucketStartUtc,
                int TotalPlays,
                int CompletedPlays,
                int TimedOutPlays,
                int UnnamedPlays,
                double AbandonmentRatePercent,
                double AverageScore);
}

