using EditorService.Application.Exceptions;
using EditorService.Application.Interfaces.CommandHandlers.SortingGames.CategoryItems;
using EditorService.Application.Interfaces.Repositories.Games;
using EditorService.Domain.Entities.SortingGames;
using EditorService.Facade.Commands.SortingGames.CategoryItems;
using EditorService.Facade.DTOs.SortingGames.CategoryItems;

namespace EditorService.Application.CommandHandlers.SortingGames.CategoryItems
{
        /// <summary>
        /// Adds an item to a category in a sorting game.
        /// </summary>
        public sealed class AddCategoryItemCommandHandler : IAddCategoryItemCommandHandler
        {
                private readonly IGameRepository _gameRepository;
                private readonly TimeProvider _timeProvider;

                /// <summary>
                /// Initializes a new instance of the <see cref="AddCategoryItemCommandHandler"/> class.
                /// </summary>
                /// <param name="gameRepository">Gets and saves games.</param>
                /// <param name="timeProvider">Gives the current server time.</param>
                public AddCategoryItemCommandHandler(IGameRepository gameRepository, TimeProvider timeProvider)
                {
                        this._gameRepository = gameRepository;
                        this._timeProvider = timeProvider;
                }

                /// <inheritdoc/>
                public async Task<CategoryItemDto> HandleAsync(AddCategoryItemCommand command, CancellationToken cancellationToken)
                {
                        // 1. Get the current time
                        DateTimeOffset now = this._timeProvider.GetUtcNow();

                        // 2. Get the sorting game
                        SortingGameEntity game = await this._gameRepository.GetSortingGameByIdAsync(id: command.GameId, cancellationToken: cancellationToken)
                        ?? throw new GameNotFoundException(gameId: command.GameId);

                        // 3. Add the item. The domain checks the lock, the values, and that the category exists in this game.
                        CategoryItemEntity item = game.AddCategoryItem(name: command.Name, explanation: command.Explanation, imageId: command.ImageId, categoryReceiverId: command.CategoryReceiverId, lockToken: command.LockToken, actorId: command.ActorId, now: now);

                        // 4. Save the game with the new item
                        await this._gameRepository.SaveChangesAsync(cancellationToken: cancellationToken);

                        // 5. Return the new item with its id
                        return new CategoryItemDto
                        {
                                Id = item.Id,
                                Name = item.Name,
                                Explanation = item.Explanation,
                                ImageId = item.ImageId,
                                CategoryReceiverId = item.CategoryReceiverId
                        };
                }
        }
}
