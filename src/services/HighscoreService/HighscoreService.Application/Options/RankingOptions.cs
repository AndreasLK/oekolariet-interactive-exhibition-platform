using System.ComponentModel.DataAnnotations;

namespace HighscoreService.Application.Options
{
        /// <summary>
        /// Shared configuration options for rolling leaderboard and ranking windows.
        /// </summary>
        public sealed class RankingOptions
        {
                /// <summary>
                /// Configuration section name in appsettings.json.
                /// </summary>
                public const string SECTION_NAME = "HighscoreRanking";

                /// <summary>
                /// Gets or sets the rolling lookback window in hours used to compute <c>RankingAtTimeOfScoring</c>.
                /// </summary>
                [Range(1, 8760)]
                public int RollingRankingWindowHours { get; set; } = 5;

                /// <summary>
                /// Gets or sets the default lookback window in hours when querying leaderboards without explicit dates.
                /// </summary>
                [Range(1, 8760)]
                public int DefaultLeaderboardWindowHours { get; set; } = 5;
        }
}

