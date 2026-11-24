using EditorService.Application.Interfaces.CommandHandlers.Games;
using EditorService.Application.Interfaces.Repositories.Games;
using EditorService.Domain.Entities.SortingGames;
using EditorService.Facade.Commands.Games;
using EditorService.Facade.DTOs.Games;

namespace EditorService.Application.CommandHandlers.Games
{
        public class CreateSortingGameCommandHandler : ICreateSortingGameCommandHandler
        {

                private readonly IGameRepository _gameRepository;
                private readonly TimeProvider _timeProvider;

                public CreateSortingGameCommandHandler(IGameRepository gameRepository, TimeProvider timeProvider)
                {
                        this._gameRepository = gameRepository;
                        this._timeProvider = timeProvider;
                }

                public async Task<GameDto> HandleAsync(CreateSortingGameCommand command, CancellationToken cancellationToken)
                {
                        // 1. Get the current time
                        DateTimeOffset now = this._timeProvider.GetUtcNow();

                        // 2. Create the game. The domain checks the title.
                        SortingGameEntity game = SortingGameEntity.Create(title: command.Title, actorId: command.ActorId, now: now);

                        // 3. Add the game and save it
                        await this._gameRepository.AddAsync(game: game, cancellationToken: cancellationToken);
                        await this._gameRepository.SaveChangesAsync(cancellationToken: cancellationToken);

                        // 4. Return what the editor needs to open the game
                        return new GameDto
                        {
                                Id = game.Id,
                                Title = game.Title,
                                Type = game.Type.ToString(),
                                Status = game.Status.ToString()
                        };
                }
        }
}
