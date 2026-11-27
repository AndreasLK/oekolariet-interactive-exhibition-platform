using HighscoreService.Facade.Commands;
using HighscoreService.Facade.Dtos;

namespace HighscoreService.Application.Interfaces.CommandHandlers
{
        /// <summary>
        /// Handles idempotent ingestion of synced highscore batches on the central Hub.
        /// </summary>
        public interface IApplySyncBatchCommandHandler
        {
                /// <summary>
                /// Applies an incoming batch of synced highscore entries idempotently.
                /// </summary>
                /// <param name="command">Batch sync command.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>Counts of received and newly applied entries.</returns>
                public Task<SyncBatchResultDto> HandleAsync(
                        ApplySyncBatchCommand command,
                        CancellationToken cancellationToken);
        }
}

