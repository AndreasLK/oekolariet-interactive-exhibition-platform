using EditorService.Application.Exceptions;
using EditorService.Application.Interfaces.CommandHandlers.SortingGames.CategoryRecievers;
using EditorService.Application.Interfaces.Repositories.Games;
using EditorService.Domain.Entities.SortingGames;
using EditorService.Facade.Commands.SortingGames.CategoryRecievers;

namespace EditorService.Application.CommandHandlers.SortingGames.CategoryRecievers
{
        /// <summary>
        /// Changes the name, color and image of a category.
        /// </summary>
        public sealed class UpdateCategoryReceiverCommandHandler : IUpdateCategoryReceiverCommandHandler
        {
                private readonly IGameRepository _gameRepository;
                private readonly TimeProvider _timeProvider;

                /// <summary>
                /// Initializes a new instance of the <see cref="UpdateCategoryReceiverCommandHandler"/> class.
                /// </summary>
                /// <param name="gameRepository">Gets and saves games.</param>
                /// <param name="timeProvider">Gives the current server time.</param>
                public UpdateCategoryReceiverCommandHandler(IGameRepository gameRepository, TimeProvider timeProvider)
                {
                        this._gameRepository = gameRepository;
                        this._timeProvider = timeProvider;
                }

                /// <inheritdoc/>
                public async Task HandleAsync(UpdateCategoryReceiverCommand command, CancellationToken cancellationToken)
                {
                        // 1. Get the current time
                        DateTimeOffset now = this._timeProvider.GetUtcNow();

                        // 2. Get the sorting game
                        SortingGameEntity game = await this._gameRepository.GetSortingGameByIdAsync(id: command.GameId, cancellationToken: cancellationToken)
                        ?? throw new GameNotFoundException(gameId: command.GameId);

                        // 3. Change the category. The domain checks the lock, that the category exists, and the new values.
                        game.UpdateCategoryReceiver(categoryReceiverId: command.CategoryReceiverId, name: command.Name, color: command.Color, imageId: command.ImageId, lockToken: command.LockToken, actorId: command.ActorId, now: now);

                        // 4. Save the change
                        await this._gameRepository.SaveChangesAsync(cancellationToken: cancellationToken);
                }
        }
}
