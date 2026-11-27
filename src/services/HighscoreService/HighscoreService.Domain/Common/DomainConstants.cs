namespace HighscoreService.Domain.Common
{
        /// <summary>
        /// Defines domain-wide validation bounds and defaults for the Highscore Service.
        /// </summary>
        public static class DomainConstants
        {
                /// <summary>
                /// Minimum allowed player age in years.
                /// </summary>
                public const int MIN_PLAYER_AGE = 0;

                /// <summary>
                /// Maximum allowed player age in years.
                /// </summary>
                public const int MAX_PLAYER_AGE = 150;

                /// <summary>
                /// Maximum allowed character length for a player's display name.
                /// </summary>
                public const int MAX_PLAYER_NAME_LENGTH = 50;

                /// <summary>
                /// Minimum allowed score value (non-negative integer).
                /// </summary>
                public const int MIN_SCORE = 0;

                /// <summary>
                /// Normalized string length of a colon-separated MAC address (AA:BB:CC:DD:EE:FF).
                /// </summary>
                public const int MAC_ADDRESS_LENGTH = 17;

                /// <summary>
                /// Minimum valid 1-based leaderboard rank.
                /// </summary>
                public const int MIN_RANK = 1;
        }
}

