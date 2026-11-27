using HighscoreService.Application.Models;
using HighscoreService.Facade.Dtos;

namespace HighscoreService.Application.Interfaces.Queries
{
        /// <summary>
        /// Generates aggregated exhibition statistics reports on the central Hub.
        /// </summary>
        public interface IHighscoreStatisticsQueryHandler
        {
                /// <summary>
                /// Generates a statistics report for the filtered dataset.
                /// </summary>
                /// <param name="query">Statistics filter DTO.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>Aggregated statistics report.</returns>
                public Task<HighscoreStatisticsReport> HandleAsync(
                        HighscoreQueryDto query,
                        CancellationToken cancellationToken);
        }
}

