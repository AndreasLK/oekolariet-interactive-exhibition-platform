using EditorService.Application.Exceptions;
using EditorService.Application.Interfaces.CommandHandlers.SortingGames.CategoryItems;
using EditorService.Application.Interfaces.Repositories.Games;
using EditorService.Domain.Entities.SortingGames;
using EditorService.Facade.Commands.SortingGames.CategoryItems;

namespace EditorService.Application.CommandHandlers.SortingGames.CategoryItems
{
        /// <summary>
        /// Removes an item from a sorting game.
        /// </summary>
        public sealed class RemoveCategoryItemCommandHandler : IRemoveCategoryItemCommandHandler
        {
                private readonly IGameRepository _gameRepository;
                private readonly TimeProvider _timeProvider;

                /// <summary>
                /// Initializes a new instance of the <see cref="RemoveCategoryItemCommandHandler"/> class.
                /// </summary>
                /// <param name="gameRepository">Gets and saves games.</param>
                /// <param name="timeProvider">Gives the current server time.</param>
                public RemoveCategoryItemCommandHandler(IGameRepository gameRepository, TimeProvider timeProvider)
                {
                        this._gameRepository = gameRepository;
                        this._timeProvider = timeProvider;
                }

                /// <inheritdoc/>
                public async Task HandleAsync(RemoveCategoryItemCommand command, CancellationToken cancellationToken)
                {
                        // 1. Get the current time
                        DateTimeOffset now = this._timeProvider.GetUtcNow();

                        // 2. Get the sorting game
                        SortingGameEntity game = await this._gameRepository.GetSortingGameByIdAsync(id: command.GameId, cancellationToken: cancellationToken)
                        ?? throw new GameNotFoundException(gameId: command.GameId);

                        // 3. Remove the item. The domain checks the lock and that the item exists.
                        game.RemoveCategoryItem(categoryItemId: command.CategoryItemId, lockToken: command.LockToken, actorId: command.ActorId, now: now);

                        // 4. Save the change
                        await this._gameRepository.SaveChangesAsync(cancellationToken: cancellationToken);
                }
        }
}
