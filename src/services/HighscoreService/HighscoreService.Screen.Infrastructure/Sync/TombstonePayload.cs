namespace HighscoreService.Screen.Infrastructure.Sync
{
        /// <summary>
        /// JSON payload returned by the Hub tombstone endpoint for deleted games.
        /// </summary>
        /// <param name="GameId">Identifier of the deleted game.</param>
        /// <param name="DeletedAtUtc">UTC timestamp when the game was deleted.</param>
        /// <param name="SyncSequence">Hub sync sequence assigned to the tombstone.</param>
        internal sealed record TombstonePayload(
                Guid GameId,
                DateTimeOffset DeletedAtUtc,
                long SyncSequence);
}

