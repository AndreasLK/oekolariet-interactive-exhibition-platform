using HighscoreService.Application.Common;
using HighscoreService.Application.Models;
using HighscoreService.Domain.Entities;
using HighscoreService.Domain.Specifications;

namespace HighscoreService.Application.Interfaces
{
        /// <summary>
        /// Core highscore persistence contract shared by both Hub and Screen hosts.
        /// </summary>
        public interface IHighscoreStore
        {
                /// <summary>
                /// Atomically computes rolling-window rank (when absent) and inserts the score idempotently by <c>Id</c>.
                /// </summary>
                /// <param name="entity">The validated entity to persist.</param>
                /// <param name="rollingRankingWindowHours">The rolling window in hours used to compute rank.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>The persisted entity and whether it was newly inserted.</returns>
                public Task<HighscoreInsertOutcome> AddIdempotentAsync(
                        HighscoreEntity entity,
                        int rollingRankingWindowHours,
                        CancellationToken cancellationToken);

                /// <summary>
                /// Queries paged highscore entries matching the specification, sorted by Score DESC, PlayedAtUtc DESC, Id ASC.
                /// </summary>
                /// <param name="specification">Filter and paging specification.</param>
                /// <param name="utcNow">Current UTC timestamp for relative windows.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>A paged list of matching entities.</returns>
                public Task<PagedResult<HighscoreEntity>> QueryAsync(
                        HighscoreSpecification specification,
                        DateTimeOffset utcNow,
                        CancellationToken cancellationToken);

                /// <summary>
                /// Soft-deletes a single highscore entry by identifier so tombstones propagate across synced instances.
                /// </summary>
                /// <param name="id">The highscore identifier.</param>
                /// <param name="deletedAtUtc">The UTC deletion timestamp.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>True if found; false otherwise.</returns>
                public Task<bool> SoftDeleteByIdAsync(
                        Guid id,
                        DateTimeOffset deletedAtUtc,
                        CancellationToken cancellationToken);

                /// <summary>
                /// Soft-deletes all highscore entries for a deleted game and records a <see cref="GameDeletionTombstone"/>.
                /// </summary>
                /// <param name="gameId">The deleted game identifier.</param>
                /// <param name="deletedAtUtc">The UTC deletion timestamp.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>The number of entries marked deleted.</returns>
                public Task<int> SoftDeleteByGameIdAsync(
                        Guid gameId,
                        DateTimeOffset deletedAtUtc,
                        CancellationToken cancellationToken);
        }
}

