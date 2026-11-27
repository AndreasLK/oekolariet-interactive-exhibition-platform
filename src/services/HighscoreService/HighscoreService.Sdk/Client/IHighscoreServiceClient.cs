using HighscoreService.Sdk.Models.Highscores.Requests;
using HighscoreService.Sdk.Models.Highscores.Responses;
using HighscoreService.Sdk.Models.Statistics.Responses;

namespace HighscoreService.Sdk.Client
{
        /// <summary>
        /// Typed HTTP client contract for consuming Highscore Service from games, kiosks, and admin dashboards.
        /// </summary>
        public interface IHighscoreServiceClient
        {
                /// <summary>
                /// Records a new highscore or abandoned-round statistic entry.
                /// </summary>
                /// <param name="request">The submission payload.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>The recorded highscore response.</returns>
                public Task<HighscoreResponse> RecordAsync(
                        RecordHighscoreRequest request,
                        CancellationToken cancellationToken = default);

                /// <summary>
                /// Queries highscores using a specification payload.
                /// </summary>
                /// <param name="query">The specification query.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>A paged list of matching highscores.</returns>
                public Task<PagedHighscoreResponse> QueryAsync(
                        HighscoreQueryRequest query,
                        CancellationToken cancellationToken = default);

                /// <summary>
                /// Retrieves a detailed statistics report matching the query filters.
                /// </summary>
                /// <param name="query">The filter specification.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>The aggregated statistics report.</returns>
                public Task<HighscoreStatisticsResponse> GetStatisticsAsync(
                        HighscoreQueryRequest query,
                        CancellationToken cancellationToken = default);

                /// <summary>
                /// Soft-deletes a single highscore entry by identifier.
                /// </summary>
                /// <param name="id">The highscore identifier.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                public Task DeleteByIdAsync(
                        Guid id,
                        CancellationToken cancellationToken = default);

                /// <summary>
                /// Soft-deletes all highscore entries for a deleted EditorService game.
                /// </summary>
                /// <param name="gameId">The deleted game identifier.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                public Task DeleteByGameIdAsync(
                        Guid gameId,
                        CancellationToken cancellationToken = default);
        }
}

