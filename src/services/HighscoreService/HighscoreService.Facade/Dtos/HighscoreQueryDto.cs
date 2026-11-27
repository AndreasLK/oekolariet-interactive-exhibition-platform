namespace HighscoreService.Facade.Dtos
{
        /// <summary>
        /// Facade query specification DTO for filtering and paging highscores.
        /// </summary>
        public sealed record HighscoreQueryDto(
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

