using ContentService.Application.Interfaces;
using ContentService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ContentService.Infrastructure.Repositories;

/// <summary>
/// Computes active image storage consumption inside SQL Server so quota checks never load metadata rows into application memory.
/// </summary>
public sealed class SqlServerStorageQuotaMonitor : IStorageQuotaMonitor
{
        /// <summary>
        /// Scoped EF Core context used to run server-side <c>SUM</c> aggregations over active image metadata.
        /// </summary>
        private readonly ContentDbContext _dbContext;

        /// <summary>
        /// Captures the request-scoped <see cref="ContentDbContext"/> so quota checks observe committed database state.
        /// </summary>
        /// <param name="dbContext">The scoped ContentService EF Core database context.</param>
        public SqlServerStorageQuotaMonitor(ContentDbContext dbContext)
        {
                ArgumentNullException.ThrowIfNull(dbContext);
                this._dbContext = dbContext;
        }

        /// <inheritdoc />
        public async Task<long> GetUsedBytesAsync(CancellationToken cancellationToken)
        {
                // Exclude soft-deleted images so reclaimed quota frees immediately, and project to long? because SQL SUM returns NULL on empty sets (which non-nullable SumAsync rejects).
                long? totalUsedBytes = await this._dbContext.Images
                        .AsNoTracking()
                        .Where(image => !image.IsDeleted)
                        .SumAsync(selector: image => (long?)image.FileSizeBytes, cancellationToken: cancellationToken);

                return totalUsedBytes ?? 0L;
        }
}
