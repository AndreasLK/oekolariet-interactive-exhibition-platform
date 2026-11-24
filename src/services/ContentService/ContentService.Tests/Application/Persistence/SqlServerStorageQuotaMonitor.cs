using ContentService.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ContentService.Tests.Application.Persistence;

/// <summary>
/// SQL Server EF Core implementation of <see cref="IStorageQuotaMonitor"/> aggregating active image byte usage in SQL.
/// </summary>
public sealed class SqlServerStorageQuotaMonitor : IStorageQuotaMonitor
{
        /// <summary>
        /// Database fixture providing fresh <see cref="TestContentDbContext"/> instances.
        /// </summary>
        private readonly MsSqlContentDatabaseFixture _fixture;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerStorageQuotaMonitor"/> class.
        /// </summary>
        /// <param name="fixture">The shared SQL Server test container fixture.</param>
        public SqlServerStorageQuotaMonitor(MsSqlContentDatabaseFixture fixture)
        {
                ArgumentNullException.ThrowIfNull(fixture);
                this._fixture = fixture;
        }

        /// <inheritdoc />
        public async Task<long> GetUsedBytesAsync(CancellationToken cancellationToken)
        {
                await using TestContentDbContext context = this._fixture.CreateDbContext();

                // Exclude soft-deleted images and project to nullable long? so SQL SUM over an empty table returns null instead of throwing InvalidOperationException.
                long? totalUsedBytes = await context.Images
                        .AsNoTracking()
                        .Where(image => !image.IsDeleted)
                        .SumAsync(image => (long?)image.FileSizeBytes, cancellationToken: cancellationToken);

                return totalUsedBytes ?? 0L;
        }
}

