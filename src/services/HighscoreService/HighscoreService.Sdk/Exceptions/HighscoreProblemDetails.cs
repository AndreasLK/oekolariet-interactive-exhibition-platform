namespace HighscoreService.Sdk.Exceptions
{
        /// <summary>
        /// RFC 7807 problem details payload returned by the Highscore Service API on errors.
        /// </summary>
        public sealed record HighscoreProblemDetails(
                string? Type,
                string? Title,
                int? Status,
                string? Detail,
                string? Instance);
}

