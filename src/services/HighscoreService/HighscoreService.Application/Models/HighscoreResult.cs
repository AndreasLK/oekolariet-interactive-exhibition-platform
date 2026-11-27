namespace HighscoreService.Application.Models
{
        /// <summary>
        /// Application-layer representation of a recorded highscore entry.
        /// </summary>
        public sealed record HighscoreResult(
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

