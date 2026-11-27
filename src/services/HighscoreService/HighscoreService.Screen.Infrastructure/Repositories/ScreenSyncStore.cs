using HighscoreService.Application.Interfaces;
using HighscoreService.Domain.Entities;
using HighscoreService.Screen.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HighscoreService.Screen.Infrastructure.Repositories
{
        /// <summary>
        /// Screen SQLite implementation of <see cref="IScreenSyncStore"/> handling outbox, watermarks, peer deltas, and 7-day pruning.
        /// </summary>
        /// <param name="dbContext">The Screen SQLite database context.</param>
        /// <param name="writeGate">The write serialization gate.</param>
        public sealed class ScreenSyncStore(
                ScreenHighscoreDbContext dbContext,
                HighscoreWriteGate writeGate) : IScreenSyncStore
        {
                private const int INCREMENTAL_VACUUM_PAGES = 64;

                private readonly ScreenHighscoreDbContext _dbContext = dbContext;
                private readonly HighscoreWriteGate _writeGate = writeGate;

                /// <inheritdoc />
                public async Task<IReadOnlyList<(HighscoreEntity Entity, long OutboxSequence)>> GetPendingOutboxAsync(
                        int batchSize,
                        CancellationToken cancellationToken)
                {
                        List<ScreenOutboxEntry> outbox = await this._dbContext.OutboxEntries
                                .AsNoTracking()
                                .OrderBy(o => o.QueuedSequence)
                                .Take(batchSize)
                                .ToListAsync(cancellationToken);

                        if (outbox.Count == 0)
                        {
                                return [];
                        }

                        List<Guid> ids = [.. outbox.Select(o => o.HighscoreId)];
                        Dictionary<Guid, HighscoreEntity> entities = await this._dbContext.Highscores
                                .AsNoTracking()
                                .Where(e => ids.Contains(e.Id))
                                .ToDictionaryAsync(e => e.Id, cancellationToken);

                        List<(HighscoreEntity, long)> result = [];
                        foreach (ScreenOutboxEntry entry in outbox)
                        {
                                if (entities.TryGetValue(entry.HighscoreId, out HighscoreEntity? entity))
                                {
                                        result.Add((entity, entry.QueuedSequence));
                                }
                        }

                        return result;
                }

                /// <inheritdoc />
                public async Task MarkOutboxSyncedAsync(
                        IReadOnlyList<(Guid HighscoreId, long OutboxSequence)> ackedItems,
                        CancellationToken cancellationToken)
                {
                        if (ackedItems.Count == 0)
                        {
                                return;
                        }

                        await this._writeGate.WaitAsync(cancellationToken);
                        try
                        {
                                List<Guid> ids = [.. ackedItems.Select(a => a.HighscoreId)];
                                List<ScreenOutboxEntry> existing = await this._dbContext.OutboxEntries
                                        .Where(o => ids.Contains(o.HighscoreId))
                                        .ToListAsync(cancellationToken);

                                Dictionary<Guid, long> pushedSeq = ackedItems.ToDictionary(a => a.HighscoreId, a => a.OutboxSequence);
                                foreach (ScreenOutboxEntry row in existing)
                                {
                                        // Only remove if no newer local mutation (e.g. soft delete) happened while HTTP push was in flight.
                                        if (pushedSeq.TryGetValue(row.HighscoreId, out long seq) && row.QueuedSequence <= seq)
                                        {
                                                this._dbContext.OutboxEntries.Remove(row);
                                        }
                                }

                                await this._dbContext.SaveChangesAsync(cancellationToken);
                        }
                        finally
                        {
                                this._writeGate.Release();
                        }
                }

                /// <inheritdoc />
                public async Task<long> GetPullWatermarkAsync(
                        string sourceNodeId,
                        Guid gameId,
                        CancellationToken cancellationToken)
                {
                        ScreenSyncWatermark? watermark = await this._dbContext.SyncWatermarks
                                .AsNoTracking()
                                .FirstOrDefaultAsync(w => w.SourceNodeId == sourceNodeId && w.GameId == gameId, cancellationToken);

                        return watermark?.LastSequence ?? 0;
                }

                /// <inheritdoc />
                public async Task SavePullWatermarkAsync(
                        string sourceNodeId,
                        Guid gameId,
                        long sequence,
                        CancellationToken cancellationToken)
                {
                        await this._writeGate.WaitAsync(cancellationToken);
                        try
                        {
                                ScreenSyncWatermark? watermark = await this._dbContext.SyncWatermarks
                                        .FirstOrDefaultAsync(w => w.SourceNodeId == sourceNodeId && w.GameId == gameId, cancellationToken);

                                if (watermark is null)
                                {
                                        this._dbContext.SyncWatermarks.Add(new ScreenSyncWatermark
                                        {
                                                SourceNodeId = sourceNodeId,
                                                GameId = gameId,
                                                LastSequence = sequence
                                        });
                                }
                                else if (sequence > watermark.LastSequence)
                                {
                                        watermark.LastSequence = sequence;
                                }

                                await this._dbContext.SaveChangesAsync(cancellationToken);
                        }
                        finally
                        {
                                this._writeGate.Release();
                        }
                }

                /// <inheritdoc />
                public async Task<int> ApplySyncedBatchAsync(
                        IReadOnlyList<HighscoreEntity> entries,
                        CancellationToken cancellationToken)
                {
                        if (entries.Count == 0)
                        {
                                return 0;
                        }

                        await this._writeGate.WaitAsync(cancellationToken);
                        try
                        {
                                int applied = 0;
                                long nextSeq = await this.GetNextLocalSequenceAsync(cancellationToken);

                                foreach (HighscoreEntity incoming in entries)
                                {
                                        bool changed = await this.ApplySingleAsync(incoming, nextSeq, cancellationToken);
                                        if (changed)
                                        {
                                                applied++;
                                                nextSeq++;
                                        }
                                }

                                if (applied > 0)
                                {
                                        await this._dbContext.SaveChangesAsync(cancellationToken);
                                }

                                return applied;
                        }
                        finally
                        {
                                this._writeGate.Release();
                        }
                }

                /// <inheritdoc />
                public async Task ApplyGameDeletionTombstonesAsync(
                        IReadOnlyList<GameDeletionTombstone> tombstones,
                        CancellationToken cancellationToken)
                {
                        if (tombstones.Count == 0)
                        {
                                return;
                        }

                        await this._writeGate.WaitAsync(cancellationToken);
                        try
                        {
                                long nextSeq = await this.GetNextLocalSequenceAsync(cancellationToken);
                                foreach (GameDeletionTombstone incoming in tombstones)
                                {
                                        await this.ApplySingleTombstoneAsync(incoming, nextSeq, cancellationToken);
                                        nextSeq++;
                                }

                                await this._dbContext.SaveChangesAsync(cancellationToken);
                        }
                        finally
                        {
                                this._writeGate.Release();
                        }
                }

                /// <inheritdoc />
                public async Task<IReadOnlyList<HighscoreEntity>> GetLocalDeltaAsync(
                        Guid? gameId,
                        long afterSequence,
                        DateTimeOffset? sinceUtc,
                        int batchSize,
                        CancellationToken cancellationToken)
                {
                        IQueryable<HighscoreEntity> query = this._dbContext.Highscores
                                .AsNoTracking()
                                .Where(e => e.SyncSequence > afterSequence);

                        if (gameId.HasValue)
                        {
                                query = query.Where(e => e.GameId == gameId.Value);
                        }

                        if (sinceUtc.HasValue)
                        {
                                DateTimeOffset utc = sinceUtc.Value.ToUniversalTime();
                                query = query.Where(e => e.PlayedAtUtc >= utc);
                        }

                        return await query
                                .OrderBy(e => e.SyncSequence)
                                .Take(batchSize)
                                .ToListAsync(cancellationToken);
                }

                /// <inheritdoc />
                public async Task<long> GetMaxLocalSequenceAsync(CancellationToken cancellationToken)
                {
                        long? max = await this._dbContext.Highscores
                                .AsNoTracking()
                                .MaxAsync(e => (long?)e.SyncSequence, cancellationToken);

                        return max ?? 0;
                }

                /// <inheritdoc />
                public async Task<int> PruneExpiredSyncedEntriesAsync(
                        DateTimeOffset cutoffUtc,
                        CancellationToken cancellationToken)
                {
                        await this._writeGate.WaitAsync(cancellationToken);
                        try
                        {
                                DateTimeOffset utc = cutoffUtc.ToUniversalTime();
                                List<Guid> outboxIds = await this._dbContext.OutboxEntries
                                        .AsNoTracking()
                                        .Select(o => o.HighscoreId)
                                        .ToListAsync(cancellationToken);

                                // Never prune unsynced outbox items or soft-delete tombstones.
                                int deleted = await this._dbContext.Highscores
                                        .Where(e => !e.IsDeleted
                                                && e.PlayedAtUtc < utc
                                                && !outboxIds.Contains(e.Id))
                                        .ExecuteDeleteAsync(cancellationToken);

                                if (deleted > 0)
                                {
                                        await this._dbContext.Database.ExecuteSqlRawAsync(
                                                $"PRAGMA incremental_vacuum({INCREMENTAL_VACUUM_PAGES});",
                                                cancellationToken);
                                }

                                return deleted;
                        }
                        finally
                        {
                                this._writeGate.Release();
                        }
                }

                private async Task<bool> ApplySingleAsync(
                        HighscoreEntity incoming,
                        long nextSeq,
                        CancellationToken cancellationToken)
                {
                        HighscoreEntity? existing = await this._dbContext.Highscores
                                .FirstOrDefaultAsync(e => e.Id == incoming.Id, cancellationToken);

                        if (existing is null)
                        {
                                GameDeletionTombstone? tombstone = await this._dbContext.GameDeletionTombstones
                                        .AsNoTracking()
                                        .FirstOrDefaultAsync(t => t.GameId == incoming.GameId, cancellationToken);

                                if (tombstone is not null && incoming.PlayedAtUtc <= tombstone.DeletedAtUtc)
                                {
                                        incoming.MarkDeleted(tombstone.DeletedAtUtc);
                                }

                                incoming.AssignSyncSequence(nextSeq);
                                this._dbContext.Highscores.Add(incoming);
                                return true;
                        }

                        if (incoming.IsDeleted && !existing.IsDeleted)
                        {
                                existing.MarkDeleted(incoming.DeletedAtUtc ?? DateTimeOffset.UtcNow);
                                existing.AssignSyncSequence(nextSeq);
                                return true;
                        }

                        return false;
                }

                private async Task ApplySingleTombstoneAsync(
                        GameDeletionTombstone incoming,
                        long nextSeq,
                        CancellationToken cancellationToken)
                {
                        GameDeletionTombstone? existing = await this._dbContext.GameDeletionTombstones
                                .FirstOrDefaultAsync(t => t.GameId == incoming.GameId, cancellationToken);

                        if (existing is null)
                        {
                                this._dbContext.GameDeletionTombstones.Add(
                                        GameDeletionTombstone.Create(incoming.GameId, incoming.DeletedAtUtc, incoming.SyncSequence));
                        }
                        else
                        {
                                existing.UpdateDeletion(incoming.DeletedAtUtc, incoming.SyncSequence);
                        }

                        List<HighscoreEntity> affected = await this._dbContext.Highscores
                                .Where(e => e.GameId == incoming.GameId && !e.IsDeleted && e.PlayedAtUtc <= incoming.DeletedAtUtc)
                                .ToListAsync(cancellationToken);

                        foreach (HighscoreEntity entry in affected)
                        {
                                entry.MarkDeleted(incoming.DeletedAtUtc);
                                entry.AssignSyncSequence(nextSeq++);
                        }
                }

                private async Task<long> GetNextLocalSequenceAsync(CancellationToken cancellationToken)
                {
                        long? max = await this._dbContext.Highscores
                                .AsNoTracking()
                                .MaxAsync(e => (long?)e.SyncSequence, cancellationToken);

                        return (max ?? 0) + 1;
                }
        }
}

