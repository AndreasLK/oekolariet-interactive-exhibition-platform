namespace HighscoreService.Sdk.Models.Statistics.Responses
{
        /// <summary>
        /// Age-bracket metrics returned in statistics reports.
        /// </summary>
        public sealed record AgeBracketStatisticsResponse(
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

