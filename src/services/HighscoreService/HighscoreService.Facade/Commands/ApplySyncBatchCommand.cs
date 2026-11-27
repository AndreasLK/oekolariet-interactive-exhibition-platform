using HighscoreService.Facade.Dtos;

namespace HighscoreService.Facade.Commands
{
        /// <summary>
        /// Command to idempotently ingest a batch of synced highscore entries onto the central Hub.
        /// </summary>
        /// <param name="Entries">Highscore entries pushed from an exhibition Screen outbox.</param>
        public sealed record ApplySyncBatchCommand(IReadOnlyList<HighscoreDto> Entries);
}

