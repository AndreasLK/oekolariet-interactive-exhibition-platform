using EditorService.Application.Exceptions;
using EditorService.Application.Interfaces.CommandHandlers.EditLocks;
using EditorService.Application.Interfaces.Repositories.Games;
using EditorService.Domain.Entities;
using EditorService.Facade.Commands.EditLocks;


namespace EditorService.Application.CommandHandlers.EditLocks
{
        /// <summary>
        /// Gives the edit lock free, so a colleague can edit the game right away.
        /// </summary>
        public sealed class ReleaseLockCommandHandler : IReleaseLockCommandHandler
        {
                private readonly IGameRepository _gameRepository;

                /// <summary>
                /// Initializes a new instance of the <see cref="ReleaseLockCommandHandler"/> class.
                /// </summary>
                /// <param name="gameRepository">Gets and saves games.</param>
                public ReleaseLockCommandHandler(IGameRepository gameRepository)
                {
                        this._gameRepository = gameRepository;
                }

                /// <inheritdoc/>
                public async Task HandleAsync(ReleaseLockCommand command, CancellationToken cancellationToken)
                {
                        // 1. Get the game, or stop with 404 if it does not exist
                        GameEntity game = await this._gameRepository.GetByIdAsync(id: command.GameId, cancellationToken: cancellationToken)
                                ?? throw new GameNotFoundException(gameId: command.GameId);

                        // 2. Release the lock. The domain does nothing if the token is wrong,
                        //    so calling this twice or with an old token is safe (idempotent).
                        game.ReleaseLock(lockToken: command.LockToken);

                        // 3. Save the change
                        await this._gameRepository.SaveChangesAsync(cancellationToken: cancellationToken);
                }
        }
}
