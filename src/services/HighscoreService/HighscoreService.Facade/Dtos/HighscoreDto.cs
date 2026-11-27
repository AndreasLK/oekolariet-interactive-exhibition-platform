namespace HighscoreService.Facade.Dtos
{
        /// <summary>
        /// Facade DTO representing a recorded highscore entry.
        /// </summary>
        public sealed record HighscoreDto(
                Guid Id,
                string? Name,
                int? Age,
                int Score,
                string SenderMacAddress,
                Guid GameId,
                DateTimeOffset PlayedAtUtc,
                int? RankingAtTimeOfScoring,
                bool IsTimedOut,
                bool IsDeleted,
                DateTimeOffset? DeletedAtUtc,
                long SyncSequence);
}

