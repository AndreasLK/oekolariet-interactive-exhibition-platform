namespace HighscoreService.Sdk.Models.Highscores.Responses
{
        /// <summary>
        /// Highscore entry returned by the Highscore Service API.
        /// </summary>
        public sealed record HighscoreResponse(
                Guid Id,
                string? Name,
                int? Age,
                int Score,
                string SenderMacAddress,
                Guid GameId,
                DateTimeOffset PlayedAtUtc,
                int? RankingAtTimeOfScoring,
                bool IsTimedOut);
}

