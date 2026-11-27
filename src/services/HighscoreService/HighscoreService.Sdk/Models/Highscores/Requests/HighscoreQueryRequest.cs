namespace HighscoreService.Sdk.Models.Highscores.Requests
{
        /// <summary>
        /// Specification request payload for querying leaderboards or statistics.
        /// </summary>
        public sealed record HighscoreQueryRequest(
                Guid? GameId = null,
                string? SenderMacAddress = null,
                DateTimeOffset? FromUtc = null,
                DateTimeOffset? ToUtc = null,
                int? LastHours = null,
                int? MinAge = null,
                int? MaxAge = null,
                bool IncludeUnnamed = false,
                bool IncludeTimedOut = false,
                int Page = 1,
                int PageSize = 50);
}

