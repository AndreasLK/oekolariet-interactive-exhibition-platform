using EditorService.Application.Exceptions;
using EditorService.Application.Interfaces.CommandHandlers.EditLocks;
using EditorService.Application.Interfaces.Repositories.Games;
using EditorService.Domain.Entities;
using EditorService.Facade.Commands.EditLocks;

namespace EditorService.Application.CommandHandlers.EditLocks
{
        /// <summary>
        /// Extends the edit lock by 15 minutes while the employee is still working.
        /// </summary>
        public sealed class RenewLockCommandHandler : IRenewLockCommandHandler
        {
                private readonly IGameRepository _gameRepository;
                private readonly TimeProvider _timeProvider;

                /// <summary>
                /// Initializes a new instance of the <see cref="RenewLockCommandHandler"/> class.
                /// </summary>
                /// <param name="gameRepository">Gets and saves games.</param>
                /// <param name="timeProvider">Gives the current server time.</param>
                public RenewLockCommandHandler(IGameRepository gameRepository, TimeProvider timeProvider)
                {
                        this._gameRepository = gameRepository;
                        this._timeProvider = timeProvider;
                }

                /// <inheritdoc/>
                public async Task HandleAsync(RenewLockCommand command, CancellationToken cancellationToken)
                {
                        // 1. Get the current time. Needed to check if the lock is still active.
                        DateTimeOffset now = this._timeProvider.GetUtcNow();

                        // 2. Get the game, or stop with 404 if it does not exist
                        GameEntity game = await this._gameRepository.GetByIdAsync(id: command.GameId, cancellationToken: cancellationToken)
                                ?? throw new GameNotFoundException(gameId: command.GameId);

                        // 3. Renew the lock. The domain throws EditLockRequiredException (409)
                        //    if the token is wrong or the lock has already expired.
                        game.RenewLock(lockToken: command.LockToken, now: now);

                        // 4. Save the new expiry time
                        await this._gameRepository.SaveChangesAsync(cancellationToken: cancellationToken);
                }
        }
}
