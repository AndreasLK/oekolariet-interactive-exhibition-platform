using EditorService.Facade.Commands.SortingGames.CategoryItems;
using EditorService.Facade.DTOs.SortingGames.CategoryItems;

namespace EditorService.Application.Interfaces.CommandHandlers.SortingGames.CategoryItems
{
        /// <summary>
        /// Adds an item to a category in a sorting game.
        /// </summary>
        public interface IAddCategoryItemCommandHandler
        {
                /// <summary>
                /// Adds the item and saves the game. The category must exist in the game.
                /// </summary>
                /// <param name="command">The game, the lock token, the item's name, explanation, image and category, and who adds it.</param>
                /// <param name="cancellationToken">Stops the call if the request is cancelled.</param>
                /// <returns>The new item with its id.</returns>
                public Task<CategoryItemDto> HandleAsync(AddCategoryItemCommand command, CancellationToken cancellationToken);
        }
}
