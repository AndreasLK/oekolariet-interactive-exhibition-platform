namespace HighscoreService.Facade.Dtos
{
        /// <summary>
        /// Facade DTO representing a deleted-game tombstone record.
        /// </summary>
        public sealed record GameDeletionTombstoneDto(
                Guid GameId,
                DateTimeOffset DeletedAtUtc,
                long SyncSequence);
}

