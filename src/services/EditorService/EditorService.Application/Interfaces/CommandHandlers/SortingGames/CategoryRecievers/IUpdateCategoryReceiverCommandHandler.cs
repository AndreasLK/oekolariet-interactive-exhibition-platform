using EditorService.Facade.Commands.SortingGames.CategoryRecievers;

namespace EditorService.Application.Interfaces.CommandHandlers.SortingGames.CategoryRecievers
{
        /// <summary>
        /// Changes the name, color and image of a category.
        /// </summary>
        public interface IUpdateCategoryReceiverCommandHandler
        {
                /// <summary>
                /// Changes the category and saves the game.
                /// </summary>
                /// <param name="command">The game, the category, the lock token, the new values and who changes it.</param>
                /// <param name="cancellationToken">Stops the call if the request is cancelled.</param>
                /// <returns>A task that completes when the category is saved.</returns>
                public Task HandleAsync(UpdateCategoryReceiverCommand command, CancellationToken cancellationToken);
        }
}
