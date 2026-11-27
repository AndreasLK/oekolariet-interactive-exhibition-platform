using HighscoreService.Application.Common;
using HighscoreService.Application.Interfaces;
using HighscoreService.Application.Models;
using HighscoreService.Domain.Entities;
using HighscoreService.Domain.Specifications;
using HighscoreService.Hub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HighscoreService.Hub.Infrastructure.Repositories
{
        /// <summary>
        /// Hub implementation of <see cref="IHighscoreStore"/> backed by SQL Server.
        /// </summary>
        /// <param name="dbContext">The Hub database context.</param>
        public sealed class HubHighscoreStore(HubHighscoreDbContext dbContext) : IHighscoreStore
        {
                private readonly HubHighscoreDbContext _dbContext = dbContext;

                /// <inheritdoc />
                public async Task<HighscoreInsertOutcome> AddIdempotentAsync(
                        HighscoreEntity entity,
                        int rollingRankingWindowHours,
                        CancellationToken cancellationToken)
                {
                        HighscoreEntity? existing = await this._dbContext.Highscores
                                .FirstOrDefaultAsync(e => e.Id == entity.Id, cancellationToken);

                        if (existing is not null)
                        {
                                return new HighscoreInsertOutcome(existing, WasInserted: false);
                        }

                        // Enforce game deletion tombstones so late arrivals for deleted games stay tombstoned.
                        GameDeletionTombstone? tombstone = await this._dbContext.GameDeletionTombstones
                                .AsNoTracking()
                                .FirstOrDefaultAsync(t => t.GameId == entity.GameId, cancellationToken);

                        if (tombstone is not null && entity.PlayedAtUtc <= tombstone.DeletedAtUtc)
                        {
                                entity.MarkDeleted(tombstone.DeletedAtUtc);
                        }

                        if (!entity.IsDeleted && !entity.RankingAtTimeOfScoring.HasValue && !entity.IsTimedOut && entity.Name is not null)
                        {
                                int rank = await this.ComputeRollingRankAsync(entity, rollingRankingWindowHours, cancellationToken);
                                entity.AssignRankingAtTimeOfScoring(rank);
                        }

                        entity.AssignSyncSequence(await this._dbContext.AllocateNextSyncSequenceAsync(cancellationToken));
                        this._dbContext.Highscores.Add(entity);
                        await this._dbContext.SaveChangesAsync(cancellationToken);

                        return new HighscoreInsertOutcome(entity, WasInserted: true);
                }

                /// <inheritdoc />
                public async Task<PagedResult<HighscoreEntity>> QueryAsync(
                        HighscoreSpecification specification,
                        DateTimeOffset utcNow,
                        CancellationToken cancellationToken)
                {
                        IQueryable<HighscoreEntity> filtered = HighscoreQueryFilter.Apply(
                                query: this._dbContext.Highscores.AsNoTracking(),
                                specification: specification,
                                utcNow: utcNow);

                        int totalCount = await filtered.CountAsync(cancellationToken);

                        List<HighscoreEntity> items = await filtered
                                .OrderByDescending(e => e.Score)
                                .ThenByDescending(e => e.PlayedAtUtc)
                                .ThenBy(e => e.Id)
                                .Skip((specification.Page - 1) * specification.PageSize)
                                .Take(specification.PageSize)
                                .ToListAsync(cancellationToken);

                        return new PagedResult<HighscoreEntity>(items, totalCount, specification.Page, specification.PageSize);
                }

                /// <inheritdoc />
                public async Task<bool> SoftDeleteByIdAsync(
                        Guid id,
                        DateTimeOffset deletedAtUtc,
                        CancellationToken cancellationToken)
                {
                        HighscoreEntity? entity = await this._dbContext.Highscores
                                .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

                        if (entity is null)
                        {
                                return false;
                        }

                        if (!entity.IsDeleted)
                        {
                                entity.MarkDeleted(deletedAtUtc);
                                entity.AssignSyncSequence(await this._dbContext.AllocateNextSyncSequenceAsync(cancellationToken));
                                await this._dbContext.SaveChangesAsync(cancellationToken);
                        }

                        return true;
                }

                /// <inheritdoc />
                public async Task<int> SoftDeleteByGameIdAsync(
                        Guid gameId,
                        DateTimeOffset deletedAtUtc,
                        CancellationToken cancellationToken)
                {
                        // WHY: Wrap tombstone creation and cascading entry soft-deletes in a single
                        // transaction so a partial failure cannot leave active entries behind a tombstone
                        // (or a tombstone without its matching entries marked deleted).
                        // Pros: Atomic state transition for admin game wipes across SQL Server.
                        // Cons: Holds row/range locks slightly longer during large game deletions.
                        await using var transaction = await this._dbContext.Database.BeginTransactionAsync(cancellationToken);

                        long seq = await this._dbContext.AllocateNextSyncSequenceAsync(cancellationToken);
                        GameDeletionTombstone? tombstone = await this._dbContext.GameDeletionTombstones
                                .FirstOrDefaultAsync(t => t.GameId == gameId, cancellationToken);

                        if (tombstone is null)
                        {
                                this._dbContext.GameDeletionTombstones.Add(GameDeletionTombstone.Create(gameId, deletedAtUtc, seq));
                        }
                        else
                        {
                                tombstone.UpdateDeletion(deletedAtUtc, seq);
                        }

                        List<HighscoreEntity> entries = await this._dbContext.Highscores
                                .Where(e => e.GameId == gameId && !e.IsDeleted)
                                .ToListAsync(cancellationToken);

                        foreach (HighscoreEntity entry in entries)
                        {
                                entry.MarkDeleted(deletedAtUtc);

                                // WHY: Each deleted entry receives its own monotonic sequence so delta-pull
                                // pagination (Take(batchSize)) never skips or splits entries sharing a sequence.
                                entry.AssignSyncSequence(await this._dbContext.AllocateNextSyncSequenceAsync(cancellationToken));
                        }

                        await this._dbContext.SaveChangesAsync(cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                        return entries.Count;
                }

                private async Task<int> ComputeRollingRankAsync(
                        HighscoreEntity entity,
                        int windowHours,
                        CancellationToken cancellationToken)
                {
                        DateTimeOffset windowStart = entity.PlayedAtUtc.AddHours(-windowHours);
                        int betterCount = await this._dbContext.Highscores
                                .AsNoTracking()
                                .Where(e => e.GameId == entity.GameId
                                        && !e.IsDeleted
                                        && !e.IsTimedOut
                                        && e.Name != null
                                        && e.PlayedAtUtc >= windowStart
                                        && (e.Score > entity.Score
                                                || (e.Score == entity.Score && e.PlayedAtUtc > entity.PlayedAtUtc)))
                                .CountAsync(cancellationToken);

                        return betterCount + 1;
                }
        }
}

