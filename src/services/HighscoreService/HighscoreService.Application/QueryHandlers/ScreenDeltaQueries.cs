using HighscoreService.Application.Common;
using HighscoreService.Application.Interfaces;
using HighscoreService.Application.Interfaces.Queries;
using HighscoreService.Domain.Entities;
using HighscoreService.Facade.Dtos;

namespace HighscoreService.Application.QueryHandlers
{
        /// <summary>
        /// Screen-only query handler serving local delta records to LAN peers.
        /// </summary>
        /// <param name="screenSyncStore">Screen sync store.</param>
        public sealed class ScreenDeltaQueries(IScreenSyncStore screenSyncStore) : IScreenDeltaQueries
        {
                private readonly IScreenSyncStore _screenSyncStore = screenSyncStore;

                /// <inheritdoc />
                public async Task<IReadOnlyList<HighscoreDto>> GetLocalDeltaAsync(
                        Guid? gameId,
                        long afterSequence,
                        DateTimeOffset? sinceUtc,
                        int batchSize,
                        CancellationToken cancellationToken)
                {
                        // Query local screen delta records for LAN peer-to-peer fallback sync.
                        IReadOnlyList<HighscoreEntity> delta = await this._screenSyncStore.GetLocalDeltaAsync(
                                gameId: gameId,
                                afterSequence: afterSequence,
                                sinceUtc: sinceUtc,
                                batchSize: batchSize,
                                cancellationToken: cancellationToken);

                        return [.. delta.Select(HighscoreDtoMapper.ToDto)];
                }
        }
}

