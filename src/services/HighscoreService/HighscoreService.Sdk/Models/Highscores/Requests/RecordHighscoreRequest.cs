namespace HighscoreService.Sdk.Models.Highscores.Requests
{
        /// <summary>
        /// Request payload to record a completed or abandoned game round.
        /// </summary>
        public sealed record RecordHighscoreRequest(
                Guid? Id,
                string? Name,
                int? Age,
                int Score,
                string SenderMacAddress,
                Guid GameId,
                DateTimeOffset? PlayedAtUtc = null,
                bool IsTimedOut = false);
}

