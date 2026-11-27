namespace HighscoreService.Application.Models
{
        /// <summary>
        /// Aggregated play and abandonment metrics grouped by exhibition screen MAC address.
        /// </summary>
        public sealed record ScreenStatistics(
                string SenderMacAddress,
                int TotalPlays,
                int CompletedPlays,
                int TimedOutPlays,
                double AbandonmentRatePercent,
                double AverageScore,
                DateTimeOffset? LastPlayedAtUtc);
}

