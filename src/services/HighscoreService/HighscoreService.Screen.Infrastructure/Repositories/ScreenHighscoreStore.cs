using HighscoreService.Application.Common;
using HighscoreService.Application.Interfaces;
using HighscoreService.Application.Models;
using HighscoreService.Domain.Entities;
using HighscoreService.Domain.Specifications;
using HighscoreService.Screen.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HighscoreService.Screen.Infrastructure.Repositories
{
        /// <summary>
        /// Screen SQLite implementation of <see cref="IHighscoreStore"/> with serialized writes and outbox queuing.
        /// </summary>
        /// <param name="dbContext">The Screen SQLite database context.</param>
        /// <param name="writeGate">The write serialization gate.</param>
        public sealed class ScreenHighscoreStore(
                ScreenHighscoreDbContext dbContext,
                HighscoreWriteGate writeGate) : IHighscoreStore
        {
                private readonly ScreenHighscoreDbContext _dbContext = dbContext;
                private readonly HighscoreWriteGate _writeGate = writeGate;

                /// <inheritdoc />
                public async Task<HighscoreInsertOutcome> AddIdempotentAsync(
                        HighscoreEntity entity,
                        int rollingRankingWindowHours,
                        CancellationToken cancellationToken)
                {
                        // Serialize writes through the process-wide gate for SQLite DELETE journal safety.
                        await this._writeGate.WaitAsync(cancellationToken);
                        try
                        {
                                // Return existing row without mutation if this client-generated Id was already recorded.
                                HighscoreEntity? existing = await this._dbContext.Highscores
                                        .FirstOrDefaultAsync(e => e.Id == entity.Id, cancellationToken);

                                if (existing is not null)
                                {
                                        return new HighscoreInsertOutcome(existing, WasInserted: false);
                                }

                                // Compute best-effort local rolling rank for named, non-timed-out plays.
                                if (!entity.RankingAtTimeOfScoring.HasValue && !entity.IsTimedOut && entity.Name is not null)
                                {
                                        int rank = await this.ComputeRollingRankAsync(entity, rollingRankingWindowHours, cancellationToken);
                                        entity.AssignRankingAtTimeOfScoring(rank);
                                }

                                // Assign monotonic local sequence and enqueue in outbox atomically.
                                long nextSeq = await this.GetNextLocalSequenceAsync(cancellationToken);
                                entity.AssignSyncSequence(nextSeq);

                                this._dbContext.Highscores.Add(entity);
                                await this.UpsertOutboxAsync(entity.Id, nextSeq, cancellationToken);
                                await this._dbContext.SaveChangesAsync(cancellationToken);

                                return new HighscoreInsertOutcome(entity, WasInserted: true);
                        }
                        finally
                        {
                                this._writeGate.Release();
                        }
                }

                /// <inheritdoc />
                public async Task<PagedResult<HighscoreEntity>> QueryAsync(
                        HighscoreSpecification specification,
                        DateTimeOffset utcNow,
                        CancellationToken cancellationToken)
                {
                        // Apply domain filters first, then compute total count and requested page slice.
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
                        await this._writeGate.WaitAsync(cancellationToken);
                        try
                        {
                                HighscoreEntity? entity = await this._dbContext.Highscores
                                        .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

                                if (entity is null)
                                {
                                        return false;
                                }

                                // Bump sync sequence and queue outbox propagation only on the first deletion.
                                if (!entity.IsDeleted)
                                {
                                        long nextSeq = await this.GetNextLocalSequenceAsync(cancellationToken);
                                        entity.MarkDeleted(deletedAtUtc);
                                        entity.AssignSyncSequence(nextSeq);
                                        await this.UpsertOutboxAsync(entity.Id, nextSeq, cancellationToken);
                                        await this._dbContext.SaveChangesAsync(cancellationToken);
                                }

                                return true;
                        }
                        finally
                        {
                                this._writeGate.Release();
                        }
                }

                /// <inheritdoc />
                public async Task<int> SoftDeleteByGameIdAsync(
                        Guid gameId,
                        DateTimeOffset deletedAtUtc,
                        CancellationToken cancellationToken)
                {
                        await this._writeGate.WaitAsync(cancellationToken);
                        try
                        {
                                // Record or advance a local game-deletion tombstone to block late LAN resurrections.
                                GameDeletionTombstone? tombstone = await this._dbContext.GameDeletionTombstones
                                        .FirstOrDefaultAsync(t => t.GameId == gameId, cancellationToken);

                                if (tombstone is null)
                                {
                                        this._dbContext.GameDeletionTombstones.Add(GameDeletionTombstone.Create(gameId, deletedAtUtc));
                                }
                                else
                                {
                                        tombstone.UpdateDeletion(deletedAtUtc, 0);
                                }

                                List<HighscoreEntity> entries = await this._dbContext.Highscores
                                        .Where(e => e.GameId == gameId && !e.IsDeleted)
                                        .ToListAsync(cancellationToken);

                                // Soft-delete each active entry and enqueue it for Hub/peer propagation.
                                long nextSeq = await this.GetNextLocalSequenceAsync(cancellationToken);
                                foreach (HighscoreEntity entry in entries)
                                {
                                        entry.MarkDeleted(deletedAtUtc);
                                        entry.AssignSyncSequence(nextSeq);
                                        await this.UpsertOutboxAsync(entry.Id, nextSeq, cancellationToken);
                                        nextSeq++;
                                }

                                await this._dbContext.SaveChangesAsync(cancellationToken);
                                return entries.Count;
                        }
                        finally
                        {
                                this._writeGate.Release();
                        }
                }

                private async Task<int> ComputeRollingRankAsync(
                        HighscoreEntity entity,
                        int windowHours,
                        CancellationToken cancellationToken)
                {
                        // Count higher scores (or newer ties) within the rolling window for rank = betterCount + 1.
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

                private async Task<long> GetNextLocalSequenceAsync(CancellationToken cancellationToken)
                {
                        // Compute max local sync sequence + 1 inside the write gate.
                        long? max = await this._dbContext.Highscores
                                .AsNoTracking()
                                .MaxAsync(e => (long?)e.SyncSequence, cancellationToken);

                        return (max ?? 0) + 1;
                }

                private async Task UpsertOutboxAsync(
                        Guid highscoreId,
                        long sequence,
                        CancellationToken cancellationToken)
                {
                        // Reuse existing outbox row if the same HighscoreId is updated before syncing.
                        ScreenOutboxEntry? existing = await this._dbContext.OutboxEntries
                                .FirstOrDefaultAsync(o => o.HighscoreId == highscoreId, cancellationToken);

                        if (existing is null)
                        {
                                this._dbContext.OutboxEntries.Add(new ScreenOutboxEntry
                                {
                                        HighscoreId = highscoreId,
                                        QueuedSequence = sequence,
                                        QueuedAtUtc = DateTimeOffset.UtcNow
                                });
                        }
                        else
                        {
                                existing.QueuedSequence = sequence;
                                existing.QueuedAtUtc = DateTimeOffset.UtcNow;
                        }
                }
        }
}

