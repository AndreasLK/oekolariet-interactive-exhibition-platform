namespace HighscoreService.Facade.Commands
{
        /// <summary>
        /// Command to soft-delete all highscore entries for a deleted game and emit a sync tombstone.
        /// </summary>
        /// <param name="GameId">Identifier of the game whose highscores should be deleted.</param>
        public sealed record DeleteGameHighscoresCommand(Guid GameId);
}

