namespace HighscoreService.Application.Models
{
        /// <summary>
        /// Aggregated play, score, and abandonment metrics for an hourly UTC time bucket.
        /// </summary>
        public sealed record TimeBucketStatistics(
                DateTimeOffset BucketStartUtc,
                int TotalPlays,
                int CompletedPlays,
                int TimedOutPlays,
                int UnnamedPlays,
                double AbandonmentRatePercent,
                double AverageScore);
}

