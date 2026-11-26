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
        /// <summary>
        /// Publishes a game, so it can be shown in the exhibition.
        /// </summary>
        public sealed class PublishGameCommandHandler : IPublishGameCommandHandler
        {
                private readonly IGameRepository _gameRepository;
                private readonly TimeProvider _timeProvider;

                /// <summary>
                /// Initializes a new instance of the <see cref="PublishGameCommandHandler"/> class.
                /// </summary>
                /// <param name="gameRepository">Gets and saves games.</param>
                /// <param name="timeProvider">Gives the current server time.</param>
                public PublishGameCommandHandler(IGameRepository gameRepository, TimeProvider timeProvider)
                {
                        this._gameRepository = gameRepository;
                        this._timeProvider = timeProvider;
                }

                /// <inheritdoc/>
                public async Task<GameDto> HandleAsync(PublishGameCommand command, CancellationToken cancellationToken)
                {
                        // 1. Get the current time. It becomes PublishedAt.
                        DateTimeOffset now = this._timeProvider.GetUtcNow();

                        // 2. Get the game, or stop with 404 if it does not exist
                        GameEntity game = await this._gameRepository.GetByIdAsync(id: command.GameId, cancellationToken: cancellationToken)
                                ?? throw new GameNotFoundException(gameId: command.GameId);

                        // 3. Publish the game. The domain checks the lock, the template,
                        //    and that the game has its content (e.g. receivers and items for a sorting game).
                        game.Publish(lockToken: command.LockToken, actorId: command.ActorId, now: now);

                        // 4. Save the change. EF Core already tracks the game.
                        await this._gameRepository.SaveChangesAsync(cancellationToken: cancellationToken);

                        // 5. Return the game, so the editor can show the new status
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
