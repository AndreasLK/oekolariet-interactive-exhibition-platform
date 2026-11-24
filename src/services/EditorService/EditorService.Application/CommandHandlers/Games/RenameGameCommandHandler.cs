using EditorService.Application.Exceptions;
using EditorService.Application.Interfaces.CommandHandlers.Games;
using EditorService.Application.Interfaces.Repositories.Games;
using EditorService.Domain.Entities;
using EditorService.Facade.Commands.Games;
using EditorService.Facade.DTOs.Games;
using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Application.CommandHandlers.Games
{
        public sealed class RenameGameCommandHandler : IRenameGameCommandHandler
        {
                private readonly IGameRepository _gameRepository;
                private readonly TimeProvider _timeProvider;

                public RenameGameCommandHandler(IGameRepository gameRepository, TimeProvider timeProvider)
                {
                        this._gameRepository = gameRepository;
                        this._timeProvider = timeProvider;
                }

                public async Task<GameDto> HandleAsync(RenameGameCommand command, CancellationToken cancellationToken)
                {
                        // 1. Get the current time
                        DateTimeOffset now = this._timeProvider.GetUtcNow();

                        // 2. Get the game, or stop with 404 if it does not exist
                        GameEntity game = await this._gameRepository.GetByIdAsync(id: command.GameId, cancellationToken: cancellationToken)
                                ?? throw new GameNotFoundException(gameId: command.GameId);

                        // 3. Rename the game. The domain checks the lock and the title.
                        game.Rename(title: command.Title, lockToken: command.LockToken, actorId: command.ActorId, now: now);

                        // 4. Save the change. EF Core already tracks the game, so no Add or Update is needed.
                        await this._gameRepository.SaveChangesAsync(cancellationToken: cancellationToken);

                        // 5. Return the updated game
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
