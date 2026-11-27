using HighscoreService.Facade.Dtos;

namespace HighscoreService.Application.Interfaces.Queries
{
        /// <summary>
        /// Queries paged leaderboards and filtered highscore entries.
        /// </summary>
        public interface IQueryHighscoresHandler
        {
                /// <summary>
                /// Executes a filtered, paged highscore query.
                /// </summary>
                /// <param name="query">Query filter and paging DTO.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>Paged highscore DTOs.</returns>
                public Task<PagedDto<HighscoreDto>> HandleAsync(
                        HighscoreQueryDto query,
                        CancellationToken cancellationToken);
        }
}

