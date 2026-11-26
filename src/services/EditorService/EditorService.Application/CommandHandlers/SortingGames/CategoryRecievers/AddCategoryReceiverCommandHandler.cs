using EditorService.Application.Exceptions;
using EditorService.Application.Interfaces.Repositories.Games;
using EditorService.Domain.Entities.SortingGames;
using EditorService.Facade.Commands.SortingGames.CategoryRecievers;
using EditorService.Facade.DTOs.SortingGames.CategoryRecievers;
using EditorService.Application.Interfaces.CommandHandlers.SortingGames.CategoryRecievers;

namespace EditorService.Application.CommandHandlers.SortingGames.CategoryRecievers
{
        /// <summary>
        /// Adds a category (receiver) to a sorting game.
        /// </summary>
        public sealed class AddCategoryReceiverCommandHandler : IAddCategoryReceiverCommandHandler
        {
                private readonly IGameRepository _gameRepository;
                private readonly TimeProvider _timeProvider;

                /// <summary>
                /// Initializes a new instance of the <see cref="AddCategoryReceiverCommandHandler"/> class.
                /// </summary>
                /// <param name="gameRepository">Gets and saves games.</param>
                /// <param name="timeProvider">Gives the current server time.</param>
                public AddCategoryReceiverCommandHandler(IGameRepository gameRepository, TimeProvider timeProvider)
                {
                        this._gameRepository = gameRepository;
                        this._timeProvider = timeProvider;
                }

                /// <inheritdoc/>
                public async Task<CategoryReceiverDto> HandleAsync(AddCategoryReceiverCommand command, CancellationToken cancellationToken)
                {
                        // 1. Get the current time
                        DateTimeOffset now = this._timeProvider.GetUtcNow();

                        // 2. Get the sorting game, or stop with 404 if it does not exist or is not a sorting game
                        SortingGameEntity game = await this._gameRepository.GetSortingGameByIdAsync(id: command.GameId, cancellationToken: cancellationToken)
                                ?? throw new GameNotFoundException(gameId: command.GameId);

                        // 3. Add the category. The domain checks the lock, the name and the color.
                        CategoryReceiverEntity receiver = game.AddCategoryReceiver(name: command.Name, color: command.Color, imageId: command.ImageId, lockToken: command.LockToken, actorId: command.ActorId, now: now);

                        // 4. Save the game. EF Core already tracks it, so the new category is saved with it.
                        await this._gameRepository.SaveChangesAsync(cancellationToken: cancellationToken);

                        // 5. Return the new category with its id
                        return new CategoryReceiverDto
                        {
                                Id = receiver.Id,
                                Name = receiver.Name,
                                Color = receiver.Color,
                                ImageId = receiver.ImageId
                        };
                }
        }
}
