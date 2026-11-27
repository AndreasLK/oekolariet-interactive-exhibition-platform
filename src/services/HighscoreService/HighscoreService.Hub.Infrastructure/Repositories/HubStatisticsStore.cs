using HighscoreService.Application.Common;
using HighscoreService.Application.Interfaces;
using HighscoreService.Domain.Entities;
using HighscoreService.Domain.Specifications;
using HighscoreService.Hub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HighscoreService.Hub.Infrastructure.Repositories
{
        /// <summary>
        /// Hub implementation of <see cref="IHubStatisticsStore"/> retrieving full-history entries.
        /// </summary>
        /// <param name="dbContext">The Hub database context.</param>
        public sealed class HubStatisticsStore(HubHighscoreDbContext dbContext) : IHubStatisticsStore
        {
                private readonly HubHighscoreDbContext _dbContext = dbContext;

                /// <inheritdoc />
                public async Task<IReadOnlyList<HighscoreEntity>> GetAllForStatisticsAsync(
                        HighscoreSpecification specification,
                        DateTimeOffset utcNow,
                        CancellationToken cancellationToken)
                {
                        // Apply specification filters without tracking so full-history aggregation stays lightweight.
                        IQueryable<HighscoreEntity> filtered = HighscoreQueryFilter.Apply(
                                query: this._dbContext.Highscores.AsNoTracking(),
                                specification: specification,
                                utcNow: utcNow);

                        return await filtered.ToListAsync(cancellationToken);
                }
        }
}

