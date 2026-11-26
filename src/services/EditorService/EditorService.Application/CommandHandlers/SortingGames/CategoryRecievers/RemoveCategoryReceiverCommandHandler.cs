using EditorService.Application.Exceptions;
using EditorService.Application.Interfaces.CommandHandlers.SortingGames.CategoryRecievers;
using EditorService.Application.Interfaces.Repositories.Games;
using EditorService.Domain.Entities.SortingGames;
using EditorService.Facade.Commands.SortingGames.CategoryRecievers;

namespace EditorService.Application.CommandHandlers.SortingGames.CategoryRecievers
{
        /// <summary>
        /// Removes a category and all its items from a sorting game.
        /// </summary>
        public sealed class RemoveCategoryReceiverCommandHandler : IRemoveCategoryReceiverCommandHandler
        {
                private readonly IGameRepository _gameRepository;
                private readonly TimeProvider _timeProvider;

                /// <summary>
                /// Initializes a new instance of the <see cref="RemoveCategoryReceiverCommandHandler"/> class.
                /// </summary>
                /// <param name="gameRepository">Gets and saves games.</param>
                /// <param name="timeProvider">Gives the current server time.</param>
                public RemoveCategoryReceiverCommandHandler(IGameRepository gameRepository, TimeProvider timeProvider)
                {
                        this._gameRepository = gameRepository;
                        this._timeProvider = timeProvider;
                }

                /// <inheritdoc/>
                public async Task HandleAsync(RemoveCategoryReceiverCommand command, CancellationToken cancellationToken)
                {
                        // 1. Get the current time
                        DateTimeOffset now = this._timeProvider.GetUtcNow();

                        // 2. Get the sorting game
                        SortingGameEntity game = await this._gameRepository.GetSortingGameByIdAsync(id: command.GameId, cancellationToken: cancellationToken)
                        ?? throw new GameNotFoundException(gameId: command.GameId);

                        // 3. Remove the category. The domain also removes all items in it.
                        game.RemoveCategoryReceiver(categoryReceiverId: command.CategoryReceiverId, lockToken: command.LockToken, actorId: command.ActorId, now: now);

                        // 4. Save the change. EF Core deletes the removed rows.
                        await this._gameRepository.SaveChangesAsync(cancellationToken: cancellationToken);
                }
        }
}
