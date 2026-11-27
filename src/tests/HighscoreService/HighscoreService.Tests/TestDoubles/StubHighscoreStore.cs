using HighscoreService.Application.Common;
using HighscoreService.Application.Interfaces;
using HighscoreService.Application.Models;
using HighscoreService.Domain.Entities;
using HighscoreService.Domain.Specifications;

namespace HighscoreService.Tests.TestDoubles
{
        /// <summary>
        /// In-memory <see cref="IHighscoreStore"/> test double capturing specifications and deletion arguments.
        /// </summary>
        internal sealed class StubHighscoreStore : IHighscoreStore
        {
                /// <summary>
                /// Gets the last <see cref="HighscoreSpecification"/> passed to <see cref="QueryAsync"/>.
                /// </summary>
                public HighscoreSpecification? LastQuerySpecification { get; private set; }

                /// <summary>
                /// Gets or sets the return value for <see cref="SoftDeleteByIdAsync"/>.
                /// </summary>
                public bool SoftDeleteByIdResult { get; set; } = true;

                /// <summary>
                /// Gets or sets the return value for <see cref="SoftDeleteByGameIdAsync"/>.
                /// </summary>
                public int SoftDeleteByGameIdResult { get; set; }

                /// <summary>
                /// Gets the entities added via <see cref="AddIdempotentAsync"/>.
                /// </summary>
                public List<HighscoreEntity> AddedEntities { get; } = [];

                /// <inheritdoc />
                public Task<HighscoreInsertOutcome> AddIdempotentAsync(
                        HighscoreEntity entity,
                        int rollingRankingWindowHours,
                        CancellationToken cancellationToken)
                {
                        // Simulate first-time insertion with rank 1 assigned.
                        entity.AssignRankingAtTimeOfScoring(1);
                        this.AddedEntities.Add(entity);
                        return Task.FromResult(new HighscoreInsertOutcome(entity, WasInserted: true));
                }

                /// <inheritdoc />
                public Task<PagedResult<HighscoreEntity>> QueryAsync(
                        HighscoreSpecification specification,
                        DateTimeOffset utcNow,
                        CancellationToken cancellationToken)
                {
                        // Capture the effective specification so handlers can be asserted without a database.
                        this.LastQuerySpecification = specification;
                        return Task.FromResult(new PagedResult<HighscoreEntity>([], 0, specification.Page, specification.PageSize));
                }

                /// <inheritdoc />
                public Task<bool> SoftDeleteByIdAsync(
                        Guid id,
                        DateTimeOffset deletedAtUtc,
                        CancellationToken cancellationToken)
                {
                        // Return the configured boolean outcome to exercise 404 vs 204 behavior.
                        return Task.FromResult(this.SoftDeleteByIdResult);
                }

                /// <inheritdoc />
                public Task<int> SoftDeleteByGameIdAsync(
                        Guid gameId,
                        DateTimeOffset deletedAtUtc,
                        CancellationToken cancellationToken)
                {
                        // Return the configured count of soft-deleted game entries.
                        return Task.FromResult(this.SoftDeleteByGameIdResult);
                }
        }
}

