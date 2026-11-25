using EditorService.Facade.Commands.Games;
using EditorService.Facade.DTOs.Games;


namespace EditorService.Application.Interfaces.CommandHandlers.Games
{
        /// <summary>
        /// Creates a new sorting game as a draft.
        /// </summary>
        public interface ICreateSortingGameCommandHandler
        {
                /// <summary>
                /// Creates the game and saves it.
                /// </summary>
                /// <param name="command">The title of the game and who creates it.</param>
                /// <param name="cancellationToken">Stops the call if the request is cancelled.</param>
                /// <returns>The basic information about the new game.</returns>
                public Task<GameDto> HandleAsync(CreateSortingGameCommand command, CancellationToken cancellationToken);
        }
}
