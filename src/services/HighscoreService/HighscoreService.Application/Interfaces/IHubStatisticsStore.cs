using HighscoreService.Domain.Entities;
using HighscoreService.Domain.Specifications;

namespace HighscoreService.Application.Interfaces
{
        /// <summary>
        /// Hub-only persistence contract for retrieving full-history entries for statistics aggregation.
        /// </summary>
        public interface IHubStatisticsStore
        {
                /// <summary>
                /// Retrieves all non-deleted entries matching a specification for statistics aggregation.
                /// </summary>
                /// <param name="specification">Filter specification.</param>
                /// <param name="utcNow">Current UTC timestamp for relative windows.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>All matching entities.</returns>
                public Task<IReadOnlyList<HighscoreEntity>> GetAllForStatisticsAsync(
                        HighscoreSpecification specification,
                        DateTimeOffset utcNow,
                        CancellationToken cancellationToken);
        }
}

