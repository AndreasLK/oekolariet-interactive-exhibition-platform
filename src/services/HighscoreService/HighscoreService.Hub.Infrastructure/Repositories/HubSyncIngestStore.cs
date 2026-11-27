using HighscoreService.Application.Interfaces;
using HighscoreService.Domain.Entities;
using HighscoreService.Hub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HighscoreService.Hub.Infrastructure.Repositories
{
        /// <summary>
        /// Hub implementation of <see cref="IHubSyncStore"/> handling batch ingestion and per-game delta serving.
        /// </summary>
        /// <param name="dbContext">The Hub database context.</param>
        public sealed class HubSyncIngestStore(HubHighscoreDbContext dbContext) : IHubSyncStore
        {
                private readonly HubHighscoreDbContext _dbContext = dbContext;

                /// <inheritdoc />
                public async Task<int> ApplySyncedBatchAsync(
                        IReadOnlyList<HighscoreEntity> entries,
                        CancellationToken cancellationToken)
                {
                        if (entries.Count == 0)
                        {
                                return 0;
                        }

                        // WHY: Preload existing entities and game tombstones in two set-based queries
                        // instead of issuing N+1 round-trips per item inside the batch loop.
                        // Pros: Predictable O(1) read round-trips regardless of batch size.
                        // Cons: Assumes batch sizes stay bounded (screen sync batches default to 100).
                        List<Guid> entryIds = entries.Select(e => e.Id).Distinct().ToList();
                        List<Guid> gameIds = entries.Select(e => e.GameId).Distinct().ToList();

                        Dictionary<Guid, HighscoreEntity> existingById = await this._dbContext.Highscores
                                .Where(e => entryIds.Contains(e.Id))
                                .ToDictionaryAsync(e => e.Id, cancellationToken);

                        Dictionary<Guid, GameDeletionTombstone> tombstonesByGameId = await this._dbContext.GameDeletionTombstones
                                .AsNoTracking()
                                .Where(t => gameIds.Contains(t.GameId))
                                .ToDictionaryAsync(t => t.GameId, cancellationToken);

                        int applied = 0;

                        foreach (HighscoreEntity incoming in entries)
                        {
                                bool changed = await this.ApplySingleAsync(
                                        incoming: incoming,
                                        existingById: existingById,
                                        tombstonesByGameId: tombstonesByGameId,
                                        cancellationToken: cancellationToken);

                                if (changed)
                                {
                                        applied++;
                                }
                        }

                        if (applied > 0)
                        {
                                await this._dbContext.SaveChangesAsync(cancellationToken);
                        }

                        return applied;
                }

                /// <inheritdoc />
                public async Task<IReadOnlyList<HighscoreEntity>> GetDeltaAsync(
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
                public async Task<IReadOnlyList<GameDeletionTombstone>> GetGameDeletionTombstonesAsync(
                        long afterSequence,
                        CancellationToken cancellationToken)
                {
                        return await this._dbContext.GameDeletionTombstones
                                .AsNoTracking()
                                .Where(t => t.SyncSequence > afterSequence)
                                .OrderBy(t => t.SyncSequence)
                                .ToListAsync(cancellationToken);
                }

                private async Task<bool> ApplySingleAsync(
                        HighscoreEntity incoming,
                        Dictionary<Guid, HighscoreEntity> existingById,
                        Dictionary<Guid, GameDeletionTombstone> tombstonesByGameId,
                        CancellationToken cancellationToken)
                {
                        if (!existingById.TryGetValue(incoming.Id, out HighscoreEntity? existing))
                        {
                                // WHY: Enforce game-level tombstones retroactively so offline screens reconnecting
                                // after an admin game wipe cannot resurrect scores played before the deletion.
                                if (tombstonesByGameId.TryGetValue(incoming.GameId, out GameDeletionTombstone? tombstone)
                                        && incoming.PlayedAtUtc <= tombstone.DeletedAtUtc)
                                {
                                        incoming.MarkDeleted(tombstone.DeletedAtUtc);
                                }

                                incoming.AssignSyncSequence(await this._dbContext.AllocateNextSyncSequenceAsync(cancellationToken));
                                this._dbContext.Highscores.Add(incoming);
                                existingById[incoming.Id] = incoming;
                                return true;
                        }

                        if (incoming.IsDeleted && !existing.IsDeleted)
                        {
                                existing.MarkDeleted(incoming.DeletedAtUtc ?? DateTimeOffset.UtcNow);
                                existing.AssignSyncSequence(await this._dbContext.AllocateNextSyncSequenceAsync(cancellationToken));
                                return true;
                        }

                        return false;
                }
        }
}

