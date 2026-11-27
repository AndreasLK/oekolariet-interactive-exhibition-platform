namespace HighscoreService.Sdk.Models.Statistics.Responses
{
        /// <summary>
        /// Rank distribution metrics returned in statistics reports.
        /// </summary>
        public sealed record RankDistributionResponse(
                int RankedPlayCount,
                int Top1Count,
                int Top3Count,
                int Top10Count,
                double Top1RatePercent,
                double Top10RatePercent,
                double? AverageRank);
}

