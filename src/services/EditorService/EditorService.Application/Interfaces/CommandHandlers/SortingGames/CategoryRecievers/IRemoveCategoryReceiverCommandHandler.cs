using EditorService.Facade.Commands.SortingGames.CategoryRecievers;

namespace EditorService.Application.Interfaces.CommandHandlers.SortingGames.CategoryRecievers
{
        /// <summary>
        /// Removes a category and all its items from a sorting game.
        /// </summary>
        public interface IRemoveCategoryReceiverCommandHandler
        {
                /// <summary>
                /// Removes the category and its items, and saves the game.
                /// </summary>
                /// <param name="command">The game, the category, the lock token and who removes it.</param>
                /// <param name="cancellationToken">Stops the call if the request is cancelled.</param>
                /// <returns>A task that completes when the category is removed.</returns>
                public Task HandleAsync(RemoveCategoryReceiverCommand command, CancellationToken cancellationToken);
        }
}
