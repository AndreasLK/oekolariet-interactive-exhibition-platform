using HighscoreService.Facade.Commands;

namespace HighscoreService.Application.Interfaces.CommandHandlers
{
        /// <summary>
        /// Handles soft-deleting all highscores belonging to a deleted game.
        /// </summary>
        public interface IDeleteGameHighscoresCommandHandler
        {
                /// <summary>
                /// Soft-deletes all highscores for the specified game and emits a sync tombstone.
                /// </summary>
                /// <param name="command">Delete-by-game command.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>The number of soft-deleted highscore entries.</returns>
                public Task<int> HandleAsync(
                        DeleteGameHighscoresCommand command,
                        CancellationToken cancellationToken);
        }
}

