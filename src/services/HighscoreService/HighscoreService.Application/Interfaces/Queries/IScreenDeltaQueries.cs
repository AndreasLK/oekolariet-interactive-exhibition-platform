using HighscoreService.Facade.Dtos;

namespace HighscoreService.Application.Interfaces.Queries
{
        /// <summary>
        /// Queries local Screen delta entries for LAN peer-to-peer synchronization.
        /// </summary>
        public interface IScreenDeltaQueries
        {
                /// <summary>
                /// Retrieves local delta entries after <paramref name="afterSequence"/>.
                /// </summary>
                /// <param name="gameId">Optional game filter.</param>
                /// <param name="afterSequence">Exclusive local sync sequence watermark.</param>
                /// <param name="sinceUtc">Optional lower timestamp bound.</param>
                /// <param name="batchSize">Maximum batch size.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>Ordered delta DTOs.</returns>
                public Task<IReadOnlyList<HighscoreDto>> GetLocalDeltaAsync(
                        Guid? gameId,
                        long afterSequence,
                        DateTimeOffset? sinceUtc,
                        int batchSize,
                        CancellationToken cancellationToken);
        }
}

