using HighscoreService.Application.Common;
using HighscoreService.Application.Interfaces;
using HighscoreService.Application.Interfaces.Queries;
using HighscoreService.Domain.Entities;
using HighscoreService.Facade.Dtos;

namespace HighscoreService.Application.QueryHandlers
{
        /// <summary>
        /// Hub-only query handler serving incremental delta records and game-deletion tombstones to Screens.
        /// </summary>
        /// <param name="hubSyncStore">Hub sync store.</param>
        public sealed class HubSyncQueries(IHubSyncStore hubSyncStore) : IHubSyncQueries
        {
                private readonly IHubSyncStore _hubSyncStore = hubSyncStore;

                /// <inheritdoc />
                public async Task<IReadOnlyList<HighscoreDto>> GetDeltaAsync(
                        Guid? gameId,
                        long afterSequence,
                        DateTimeOffset? sinceUtc,
                        int batchSize,
                        CancellationToken cancellationToken)
                {
                        // Fetch ordered highscore changes strictly after the caller's watermark sequence.
                        IReadOnlyList<HighscoreEntity> delta = await this._hubSyncStore.GetDeltaAsync(
                                gameId: gameId,
                                afterSequence: afterSequence,
                                sinceUtc: sinceUtc,
                                batchSize: batchSize,
                                cancellationToken: cancellationToken);

                        return [.. delta.Select(HighscoreDtoMapper.ToDto)];
                }

                /// <inheritdoc />
                public async Task<IReadOnlyList<GameDeletionTombstoneDto>> GetGameDeletionTombstonesAsync(
                        long afterSequence,
                        CancellationToken cancellationToken)
                {
                        // Return game-deletion tombstones so offline screens purge scores for deleted games.
                        IReadOnlyList<GameDeletionTombstone> tombstones = await this._hubSyncStore.GetGameDeletionTombstonesAsync(
                                afterSequence: afterSequence,
                                cancellationToken: cancellationToken);

                        return
                        [
                                .. tombstones.Select(t => new GameDeletionTombstoneDto(
                                        GameId: t.GameId,
                                        DeletedAtUtc: t.DeletedAtUtc,
                                        SyncSequence: t.SyncSequence))
                        ];
                }
        }
}

