namespace HighscoreService.Sdk.Models.Highscores.Responses
{
        /// <summary>
        /// Paged list of highscore entries returned by the Highscore Service API.
        /// </summary>
        public sealed record PagedHighscoreResponse(
                IReadOnlyList<HighscoreResponse> Items,
                int TotalCount,
                int Page,
                int PageSize);
}

