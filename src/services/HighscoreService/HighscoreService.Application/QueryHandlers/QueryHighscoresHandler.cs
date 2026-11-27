using HighscoreService.Application.Common;
using HighscoreService.Application.Interfaces;
using HighscoreService.Application.Interfaces.Queries;
using HighscoreService.Application.Options;
using HighscoreService.Domain.Entities;
using HighscoreService.Domain.Specifications;
using HighscoreService.Facade.Dtos;
using Microsoft.Extensions.Options;

namespace HighscoreService.Application.QueryHandlers
{
        /// <summary>
        /// Executes filtered, paged highscore queries sorted by score and recency.
        /// </summary>
        /// <param name="store">Highscore persistence store.</param>
        /// <param name="options">Ranking configuration options.</param>
        /// <param name="timeProvider">Server clock provider.</param>
        public sealed class QueryHighscoresHandler(
                IHighscoreStore store,
                IOptions<RankingOptions> options,
                TimeProvider timeProvider) : IQueryHighscoresHandler
        {
                private readonly IHighscoreStore _store = store;
                private readonly RankingOptions _options = options.Value;
                private readonly TimeProvider _timeProvider = timeProvider;

                /// <inheritdoc />
                public async Task<PagedDto<HighscoreDto>> HandleAsync(
                        HighscoreQueryDto query,
                        CancellationToken cancellationToken)
                {
                        HighscoreSpecification effectiveSpec = HighscoreDtoMapper.ToSpecification(query);
                        if (!effectiveSpec.FromUtc.HasValue && !effectiveSpec.ToUtc.HasValue && !effectiveSpec.LastHours.HasValue)
                        {
                                // Default to the rolling leaderboard window when no explicit time range is supplied.
                                effectiveSpec = effectiveSpec with
                                {
                                        LastHours = this._options.DefaultLeaderboardWindowHours
                                };
                        }

                        PagedResult<HighscoreEntity> paged = await this._store.QueryAsync(
                                specification: effectiveSpec.NormalizePaging(),
                                utcNow: this._timeProvider.GetUtcNow(),
                                cancellationToken: cancellationToken);

                        IReadOnlyList<HighscoreDto> items =
                        [
                                .. paged.Items.Select(HighscoreDtoMapper.ToDto)
                        ];

                        return new PagedDto<HighscoreDto>(
                                Items: items,
                                TotalCount: paged.TotalCount,
                                Page: paged.Page,
                                PageSize: paged.PageSize);
                }
        }
}

