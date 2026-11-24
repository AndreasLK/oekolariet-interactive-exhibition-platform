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
        /// Changes the theme of an existing game.
        /// </summary>
        public sealed class UpdateGameThemeCommandHandler : IUpdateGameThemeCommandHandler
        {
                private readonly IGameRepository _gameRepository;
                private readonly TimeProvider _timeProvider;

                /// <summary>
                /// Initializes a new instance of the <see cref="UpdateGameThemeCommandHandler"/> class.
                /// </summary>
                /// <param name="gameRepository">Gets and saves games.</param>
                /// <param name="timeProvider">Gives the current server time.</param>
                public UpdateGameThemeCommandHandler(IGameRepository gameRepository, TimeProvider timeProvider)
                {
                        this._gameRepository = gameRepository;
                        this._timeProvider = timeProvider;
                }

                /// <inheritdoc/>
                public async Task HandleAsync(UpdateGameThemeCommand command, CancellationToken cancellationToken)
                {
                        // 1. Get the current time
                        DateTimeOffset now = this._timeProvider.GetUtcNow();

                        // 2. Get the game, or stop with 404 if it does not exist
                        GameEntity game = await this._gameRepository.GetByIdAsync(id: command.GameId, cancellationToken: cancellationToken)
                                ?? throw new GameNotFoundException(gameId: command.GameId);

                        // 3. Change the theme. The domain checks the lock and that the game is a draft.
                        game.ChangeTheme(themeId: command.ThemeId, lockToken: command.LockToken, actorId: command.ActorId, now: now);

                        // 4. Save the change. EF Core already tracks the game.
                        await this._gameRepository.SaveChangesAsync(cancellationToken: cancellationToken);
                }
        }
}
