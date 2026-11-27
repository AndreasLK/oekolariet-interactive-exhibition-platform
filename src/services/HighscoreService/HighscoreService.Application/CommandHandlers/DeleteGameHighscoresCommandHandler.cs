using HighscoreService.Application.Interfaces;
using HighscoreService.Application.Interfaces.CommandHandlers;
using HighscoreService.Domain.Exceptions;
using HighscoreService.Facade.Commands;

namespace HighscoreService.Application.CommandHandlers
{
        /// <summary>
        /// Soft-deletes all highscore entries for a deleted EditorService game and records a tombstone.
        /// </summary>
        /// <param name="store">Highscore persistence store.</param>
        /// <param name="timeProvider">Server clock provider.</param>
        public sealed class DeleteGameHighscoresCommandHandler(
                IHighscoreStore store,
                TimeProvider timeProvider) : IDeleteGameHighscoresCommandHandler
        {
                private readonly IHighscoreStore _store = store;
                private readonly TimeProvider _timeProvider = timeProvider;

                /// <inheritdoc />
                public Task<int> HandleAsync(
                        DeleteGameHighscoresCommand command,
                        CancellationToken cancellationToken)
                {
                        // Reject empty GameId before attempting bulk soft-delete or tombstone creation.
                        if (command.GameId == Guid.Empty)
                        {
                                throw new DomainValidationException("GameId cannot be empty.");
                        }

                        // Soft-delete matching entries and record a tombstone using the authoritative server clock.
                        return this._store.SoftDeleteByGameIdAsync(
                                gameId: command.GameId,
                                deletedAtUtc: this._timeProvider.GetUtcNow(),
                                cancellationToken: cancellationToken);
                }
        }
}

