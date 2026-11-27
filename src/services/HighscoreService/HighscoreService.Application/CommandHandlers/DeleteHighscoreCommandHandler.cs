using HighscoreService.Application.Interfaces;
using HighscoreService.Application.Interfaces.CommandHandlers;
using HighscoreService.Domain.Exceptions;
using HighscoreService.Facade.Commands;

namespace HighscoreService.Application.CommandHandlers
{
        /// <summary>
        /// Soft-deletes a single highscore entry so tombstones propagate across synced instances.
        /// </summary>
        /// <param name="store">Highscore persistence store.</param>
        /// <param name="timeProvider">Server clock provider.</param>
        public sealed class DeleteHighscoreCommandHandler(
                IHighscoreStore store,
                TimeProvider timeProvider) : IDeleteHighscoreCommandHandler
        {
                private readonly IHighscoreStore _store = store;
                private readonly TimeProvider _timeProvider = timeProvider;

                /// <inheritdoc />
                public async Task HandleAsync(
                        DeleteHighscoreCommand command,
                        CancellationToken cancellationToken)
                {
                        // Reject empty identifier so we fail fast with 400 rather than querying storage.
                        if (command.Id == Guid.Empty)
                        {
                                throw new DomainValidationException("Highscore Id cannot be empty.");
                        }

                        // Soft-delete rather than hard-delete so synced nodes receive the deletion.
                        bool deleted = await this._store.SoftDeleteByIdAsync(
                                id: command.Id,
                                deletedAtUtc: this._timeProvider.GetUtcNow(),
                                cancellationToken: cancellationToken);

                        if (!deleted)
                        {
                                throw new EntityNotFoundException($"Highscore '{command.Id}' was not found.");
                        }
                }
        }
}

