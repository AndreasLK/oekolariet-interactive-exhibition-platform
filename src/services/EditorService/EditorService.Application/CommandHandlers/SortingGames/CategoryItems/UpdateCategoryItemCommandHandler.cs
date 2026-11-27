using EditorService.Application.Interfaces.CommandHandlers.SortingGames.CategoryItems;
using EditorService.Application.Interfaces.Repositories.Games;
using EditorService.Domain.Entities.SortingGames;
using EditorService.Facade.Commands.SortingGames.CategoryItems;
using EditorService.Application.Exceptions;


namespace EditorService.Application.CommandHandlers.SortingGames
{
        /// <summary>
        /// Changes the name, explanation and image of an item. The item stays in its category.
        /// </summary>
        public sealed class UpdateCategoryItemCommandHandler : IUpdateCategoryItemCommandHandler
        {
                private readonly IGameRepository _gameRepository;
                private readonly TimeProvider _timeProvider;

                /// <summary>
                /// Initializes a new instance of the <see cref="UpdateCategoryItemCommandHandler"/> class.
                /// </summary>
                /// <param name="gameRepository">Gets and saves games.</param>
                /// <param name="timeProvider">Gives the current server time.</param>
                public UpdateCategoryItemCommandHandler(IGameRepository gameRepository, TimeProvider timeProvider)
                {
                        this._gameRepository = gameRepository;
                        this._timeProvider = timeProvider;
                }

                /// <inheritdoc/>
                public async Task HandleAsync(UpdateCategoryItemCommand command, CancellationToken cancellationToken)
                {
                        // 1. Get the current time
                        DateTimeOffset now = this._timeProvider.GetUtcNow();

                        // 2. Get the sorting game
                        SortingGameEntity game = await this._gameRepository.GetSortingGameByIdAsync(id: command.GameId, cancellationToken: cancellationToken)
                        ?? throw new GameNotFoundException(gameId: command.GameId);

                        // 3. Change the item. The domain checks the lock, that the item exists, and the new values.
                        game.UpdateCategoryItem(categoryItemId: command.CategoryItemId, name: command.Name, explanation: command.Explanation, imageId: command.ImageId, lockToken: command.LockToken, actorId: command.ActorId, now: now);

                        // 4. Save the change
                        await this._gameRepository.SaveChangesAsync(cancellationToken: cancellationToken);
                }
        }
}
