namespace HighscoreService.Application.Models
{
        /// <summary>
        /// Summarizes how often players achieved top leaderboard ranks at the time of scoring.
        /// </summary>
        public sealed record RankDistributionStatistics(
                int RankedPlayCount,
                int Top1Count,
                int Top3Count,
                int Top10Count,
                double Top1RatePercent,
                double Top10RatePercent,
                double? AverageRank);
}

