using EditorService.Facade.Commands.SortingGames.CategoryItems;

namespace EditorService.Application.Interfaces.CommandHandlers.SortingGames.CategoryItems
{
        /// <summary>
        /// Removes an item from a sorting game.
        /// </summary>
        public interface IRemoveCategoryItemCommandHandler
        {
                /// <summary>
                /// Removes the item and saves the game.
                /// </summary>
                /// <param name="command">The game, the item, the lock token and who removes it.</param>
                /// <param name="cancellationToken">Stops the call if the request is cancelled.</param>
                /// <returns>A task that completes when the item is removed.</returns>
                public Task HandleAsync(RemoveCategoryItemCommand command, CancellationToken cancellationToken);
        }
}
