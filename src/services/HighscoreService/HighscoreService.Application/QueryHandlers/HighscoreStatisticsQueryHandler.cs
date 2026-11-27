using HighscoreService.Application.Common;
using HighscoreService.Application.Interfaces;
using HighscoreService.Application.Interfaces.Queries;
using HighscoreService.Application.Models;
using HighscoreService.Application.UseCases;
using HighscoreService.Domain.Entities;
using HighscoreService.Domain.Specifications;
using HighscoreService.Facade.Dtos;

namespace HighscoreService.Application.QueryHandlers
{
        /// <summary>
        /// Hub-only query handler generating comprehensive exhibition statistics reports.
        /// </summary>
        /// <param name="statisticsStore">Hub statistics store.</param>
        /// <param name="timeProvider">Server clock provider.</param>
        public sealed class HighscoreStatisticsQueryHandler(
                IHubStatisticsStore statisticsStore,
                TimeProvider timeProvider) : IHighscoreStatisticsQueryHandler
        {
                private readonly IHubStatisticsStore _statisticsStore = statisticsStore;
                private readonly TimeProvider _timeProvider = timeProvider;

                /// <inheritdoc />
                public async Task<HighscoreStatisticsReport> HandleAsync(
                        HighscoreQueryDto query,
                        CancellationToken cancellationToken)
                {
                        // Statistics always include unnamed and timed-out plays so completion rates are accurate.
                        HighscoreSpecification statsSpec = HighscoreDtoMapper.ToSpecification(query) with
                        {
                                IncludeUnnamed = true,
                                IncludeTimedOut = true,
                                IncludeDeleted = false
                        };

                        DateTimeOffset now = this._timeProvider.GetUtcNow();
                        IReadOnlyList<HighscoreEntity> entries = await this._statisticsStore.GetAllForStatisticsAsync(
                                specification: statsSpec,
                                utcNow: now,
                                cancellationToken: cancellationToken);

                        return HighscoreStatisticsCalculator.BuildReport(
                                entries: entries,
                                generatedAtUtc: now);
                }
        }
}

