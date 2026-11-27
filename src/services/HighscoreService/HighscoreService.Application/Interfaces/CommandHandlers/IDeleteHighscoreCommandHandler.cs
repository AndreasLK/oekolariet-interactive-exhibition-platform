using HighscoreService.Facade.Commands;

namespace HighscoreService.Application.Interfaces.CommandHandlers
{
        /// <summary>
        /// Handles soft-deleting a single highscore entry.
        /// </summary>
        public interface IDeleteHighscoreCommandHandler
        {
                /// <summary>
                /// Soft-deletes the specified highscore entry so tombstones propagate across synced nodes.
                /// </summary>
                /// <param name="command">Delete command containing the highscore identifier.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                public Task HandleAsync(
                        DeleteHighscoreCommand command,
                        CancellationToken cancellationToken);
        }
}

