using EditorService.Application.Exceptions;
using EditorService.Application.Interfaces.Repositories.Games;
using EditorService.Domain.Entities;
using EditorService.Facade.Commands.EditLocks;
using EditorService.Facade.DTOs.EditLocks;
using EditorService.Application.Interfaces.CommandHandlers.EditLocks;
using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Application.CommandHandlers.EditLocks
{
        /// <summary>
        /// Takes the edit lock on a game, so only one employee can edit it at a time.
        /// </summary>
        public sealed class AcquireLockCommandHandler : IAcquireLockCommandHandler
        {
                private readonly IGameRepository _gameRepository;
                private readonly TimeProvider _timeProvider;

                /// <summary>
                /// Initializes a new instance of the <see cref="AcquireLockCommandHandler"/> class.
                /// </summary>
                /// <param name="gameRepository">Gets and saves games.</param>
                /// <param name="timeProvider">Gives the current server time.</param>
                public AcquireLockCommandHandler(IGameRepository gameRepository, TimeProvider timeProvider)
                {
                        this._gameRepository = gameRepository;
                        this._timeProvider = timeProvider;
                }

                /// <inheritdoc/>
                public async Task<EditLockDto> HandleAsync(AcquireLockCommand command, CancellationToken cancellationToken)
                {
                        // 1. Get the current time. The lock expires 15 minutes from now.
                        DateTimeOffset now = this._timeProvider.GetUtcNow();

                        // 2. Get the game, or stop with 404 if it does not exist
                        GameEntity game = await this._gameRepository.GetByIdAsync(id: command.GameId, cancellationToken: cancellationToken)
                                ?? throw new GameNotFoundException(gameId: command.GameId);

                        // 3. Take the lock. The domain throws GameLockedException (423) if someone else holds it,
                        //    and renews it if the same employee on the same device already holds it.
                        Guid lockToken = game.AcquireLock(holderName: command.HolderName, deviceName: command.DeviceName, now: now);

                        // 4. Save the lock on the game
                        await this._gameRepository.SaveChangesAsync(cancellationToken: cancellationToken);

                        // 5. Return the token and when it expires. EditLock is never null right after AcquireLock.
                        return new EditLockDto
                        {
                                LockToken = lockToken,
                                ExpiresAt = game.EditLock!.ExpiresAt
                        };
                }
        }
}
