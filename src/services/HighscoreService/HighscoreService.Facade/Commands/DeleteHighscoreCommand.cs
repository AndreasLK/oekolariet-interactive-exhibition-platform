namespace HighscoreService.Facade.Commands
{
        /// <summary>
        /// Command to soft-delete a single highscore entry by identifier.
        /// </summary>
        /// <param name="Id">Unique identifier of the highscore entry to delete.</param>
        public sealed record DeleteHighscoreCommand(Guid Id);
}

