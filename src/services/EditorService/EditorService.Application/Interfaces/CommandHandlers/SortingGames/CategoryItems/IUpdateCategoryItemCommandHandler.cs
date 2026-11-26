using EditorService.Facade.Commands.SortingGames.CategoryItems;

namespace EditorService.Application.Interfaces.CommandHandlers.SortingGames.CategoryItems
{
        /// <summary>
        /// Changes the name, explanation and image of an item. The item stays in its category.
        /// </summary>
        public interface IUpdateCategoryItemCommandHandler
        {
                /// <summary>
                /// Changes the item and saves the game.
                /// </summary>
                /// <param name="command">The game, the item, the lock token, the new values and who changes it.</param>
                /// <param name="cancellationToken">Stops the call if the request is cancelled.</param>
                /// <returns>A task that completes when the item is saved.</returns>
                public Task HandleAsync(UpdateCategoryItemCommand command, CancellationToken cancellationToken);
        }
}
