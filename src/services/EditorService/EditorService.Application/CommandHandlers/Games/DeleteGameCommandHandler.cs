using EditorService.Application.Exceptions;
using EditorService.Application.Interfaces.CommandHandlers.Games;
using EditorService.Application.Interfaces.Repositories.Games;
using EditorService.Domain.Entities;
using EditorService.Facade.Commands.Games;
using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Application.CommandHandlers.Games
{
        /// <summary>
        /// Deletes an existing game with all its content.
        /// </summary>
        public sealed class DeleteGameCommandHandler : IDeleteGameCommandHandler
        {
                private readonly IGameRepository _gameRepository;
                private readonly TimeProvider _timeProvider;

                /// <summary>
                /// Initializes a new instance of the <see cref="DeleteGameCommandHandler"/> class.
                /// </summary>
                /// <param name="gameRepository">Gets and deletes games.</param>
                /// <param name="timeProvider">Gives the current server time.</param>
                public DeleteGameCommandHandler(IGameRepository gameRepository, TimeProvider timeProvider)
                {
                        this._gameRepository = gameRepository;
                        this._timeProvider = timeProvider;
                }

                /// <inheritdoc/>
                public async Task HandleAsync(DeleteGameCommand command, CancellationToken cancellationToken)
                {
                        // 1. Get the current time. Needed to check if the lock has expired.
                        DateTimeOffset now = this._timeProvider.GetUtcNow();

                        // 2. Get the game, or stop with 404 if it does not exist
                        GameEntity game = await this._gameRepository.GetByIdAsync(id: command.GameId, cancellationToken: cancellationToken)
                                ?? throw new GameNotFoundException(gameId: command.GameId);

                        // 3. Ask the domain if the game may be deleted. It checks the lock.
                        game.EnsureCanBeDeleted(lockToken: command.LockToken, now: now);

                        // 4. Remove the game. Receivers and items go with it, because they are owned by the game.
                        await this._gameRepository.DeleteAsync(game: game, cancellationToken: cancellationToken);
                        await this._gameRepository.SaveChangesAsync(cancellationToken: cancellationToken);
                }
        }
}
