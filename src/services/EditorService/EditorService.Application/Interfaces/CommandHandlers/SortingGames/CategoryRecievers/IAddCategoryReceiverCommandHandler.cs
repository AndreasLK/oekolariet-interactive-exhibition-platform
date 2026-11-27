using EditorService.Facade.Commands.SortingGames.CategoryRecievers;
using EditorService.Facade.DTOs.SortingGames.CategoryRecievers;

namespace EditorService.Application.Interfaces.CommandHandlers.SortingGames.CategoryRecievers
{
        /// <summary>
        /// Adds a category (receiver) to a sorting game.
        /// </summary>
        public interface IAddCategoryReceiverCommandHandler
        {
                /// <summary>
                /// Adds the category and saves the game.
                /// </summary>
                /// <param name="command">The game, the lock token, the category's name, color and image, and who adds it.</param>
                /// <param name="cancellationToken">Stops the call if the request is cancelled.</param>
                /// <returns>The new category with its id.</returns>
                public Task<CategoryReceiverDto> HandleAsync(AddCategoryReceiverCommand command, CancellationToken cancellationToken);
        }
}
