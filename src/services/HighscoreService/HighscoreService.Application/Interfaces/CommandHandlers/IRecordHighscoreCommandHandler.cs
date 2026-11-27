using HighscoreService.Facade.Commands;
using HighscoreService.Facade.Dtos;

namespace HighscoreService.Application.Interfaces.CommandHandlers
{
        /// <summary>
        /// Handles recording a new highscore or abandoned-round entry.
        /// </summary>
        public interface IRecordHighscoreCommandHandler
        {
                /// <summary>
                /// Validates and persists a highscore entry idempotently, returning the resulting DTO.
                /// </summary>
                /// <param name="command">The highscore submission command.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>The persisted highscore DTO.</returns>
                public Task<HighscoreDto> HandleAsync(
                        RecordHighscoreCommand command,
                        CancellationToken cancellationToken);
        }
}

