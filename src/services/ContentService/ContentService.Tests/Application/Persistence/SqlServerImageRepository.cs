using ContentService.Application.Interfaces;
using ContentService.Application.Common;
using ContentService.Domain.Entities;
using ContentService.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ContentService.Tests.Application.Persistence;

/// <summary>
/// SQL Server EF Core implementation of <see cref="IImageRepository"/> executed against a real Testcontainers MSSQL instance.
/// </summary>
public sealed class SqlServerImageRepository : IImageRepository
{
        /// <summary>
        /// Database fixture providing fresh <see cref="TestContentDbContext"/> instances.
        /// </summary>
        private readonly MsSqlContentDatabaseFixture _fixture;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerImageRepository"/> class.
        /// </summary>
        /// <param name="fixture">The shared SQL Server test container fixture.</param>
        public SqlServerImageRepository(MsSqlContentDatabaseFixture fixture)
        {
                ArgumentNullException.ThrowIfNull(fixture);
                this._fixture = fixture;
        }

        /// <inheritdoc />
        public async Task<ImageEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
                await using TestContentDbContext context = this._fixture.CreateDbContext();

                // Query only metadata with AsNoTracking so binary payloads are never materialized here.
                return await context.Images
                        .AsNoTracking()
                        .FirstOrDefaultAsync(image => image.Id == id, cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ImageEntity>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
        {
                // Short-circuit empty batches before opening a DbContext or issuing an empty WHERE IN query.
                if (ids.Count == 0)
                {
                        return [];
                }

                await using TestContentDbContext context = this._fixture.CreateDbContext();
                return await context.Images
                        .AsNoTracking()
                        .Where(image => ids.Contains(image.Id))
                        .ToListAsync(cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public async Task<ImageEntity?> GetBySha256Async(string sha256Hash, CancellationToken cancellationToken)
        {
                await using TestContentDbContext context = this._fixture.CreateDbContext();

                // Exclude soft-deleted images so deleted uploads are never reused for content-hash deduplication.
                return await context.Images
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                                image => !image.IsDeleted && image.Sha256Hash == sha256Hash,
                                cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public async Task<PagedResult<ImageEntity>> ListAsync(
                int page,
                int pageSize,
                string? searchTerm,
                CancellationToken cancellationToken)
        {
                await using TestContentDbContext context = this._fixture.CreateDbContext();

                // Filter out soft-deleted images before counting and paging active catalog records.
                IQueryable<ImageEntity> query = context.Images
                        .AsNoTracking()
                        .Where(image => !image.IsDeleted);

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                        // Use ToLower().Contains(...) rather than StringComparison.OrdinalIgnoreCase so EF Core translates it to SQL.
                        string normalizedSearch = searchTerm.Trim().ToLowerInvariant();
                        query = query.Where(image => image.FileName.ToLower().Contains(normalizedSearch));
                }

                int totalCount = await query.CountAsync(cancellationToken: cancellationToken);

                // Multiply in 64-bit arithmetic and clamp to int.MaxValue to avoid arithmetic overflow on extreme page indices.
                int skipCount = (int)Math.Min(int.MaxValue, (long)(page - 1) * pageSize);

                // Sort by CreatedAt with Id as a deterministic tie-breaker for stable pagination across equal timestamps.
                List<ImageEntity> items = await query
                        .OrderBy(image => image.CreatedAt)
                        .ThenBy(image => image.Id)
                        .Skip(skipCount)
                        .Take(pageSize)
                        .ToListAsync(cancellationToken: cancellationToken);

                return new PagedResult<ImageEntity>
                {
                        Items = items,
                        Page = page,
                        PageSize = pageSize,
                        TotalCount = totalCount
                };
        }

        /// <inheritdoc />
        public async Task<bool> IsReferencedByAnyTemplateAsync(Guid imageId, CancellationToken cancellationToken)
        {
                await using TestContentDbContext context = this._fixture.CreateDbContext();

                // Check TemplateImageReferences across all persisted templates including archived ones so draft references never break.
                return await context.TemplateImageReferences
                        .AsNoTracking()
                        .AnyAsync(reference => reference.ImageId == imageId, cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public async Task AddAsync(ImageEntity metadata, ImageBinaryEntity binary, CancellationToken cancellationToken)
        {
                await using TestContentDbContext context = this._fixture.CreateDbContext();

                // Persist metadata and binary rows together in a single transaction so neither exists without the other.
                context.Images.Add(metadata);
                context.ImageBinaries.Add(binary);
                await context.SaveChangesAsync(cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public async Task DeleteAsync(ImageEntity image, Guid expectedConcurrencyStamp, CancellationToken cancellationToken)
        {
                await using TestContentDbContext context = this._fixture.CreateDbContext();

                // Update atomically with concurrency stamp predicate so stale concurrent deletions fail reliably in SQL Server.
                int rowsAffected = await context.Images
                        .Where(existing => existing.Id == image.Id && existing.ConcurrencyStamp == expectedConcurrencyStamp)
                        .ExecuteUpdateAsync(
                                setters => setters
                                        .SetProperty(existing => existing.IsDeleted, image.IsDeleted)
                                        .SetProperty(existing => existing.UpdatedAt, image.UpdatedAt)
                                        .SetProperty(existing => existing.UpdatedBy, image.UpdatedBy)
                                        .SetProperty(existing => existing.ConcurrencyStamp, image.ConcurrencyStamp),
                                cancellationToken: cancellationToken);

                // Zero affected rows indicates either a missing row or a stale ConcurrencyStamp.
                if (rowsAffected == 0)
                {
                        throw new ConcurrencyConflictException($"Image '{image.Id}' was modified concurrently.");
                }
        }

        /// <summary>
        /// Counts all image metadata rows currently persisted in SQL Server.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The total row count in the <c>Images</c> table.</returns>
        public async Task<int> CountStoredImagesAsync(CancellationToken cancellationToken)
        {
                await using TestContentDbContext context = this._fixture.CreateDbContext();

                // Count every persisted metadata row, including soft-deleted ones, to verify database side effects in tests.
                return await context.Images.CountAsync(cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Loads a persisted <see cref="ImageBinaryEntity"/> row directly from SQL Server for upload verification.
        /// </summary>
        /// <param name="imageId">The image identifier.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The stored <see cref="ImageBinaryEntity"/>, or <see langword="null"/> if absent.</returns>
        public async Task<ImageBinaryEntity?> GetStoredBinaryAsync(Guid imageId, CancellationToken cancellationToken)
        {
                await using TestContentDbContext context = this._fixture.CreateDbContext();

                // Read the binary row directly without change tracking to assert exact byte storage after upload.
                return await context.ImageBinaries
                        .AsNoTracking()
                        .FirstOrDefaultAsync(binary => binary.ImageId == imageId, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Seeds an <see cref="ImageEntity"/> metadata row without a corresponding <see cref="ImageBinaryEntity"/> row to test missing-stream handling.
        /// </summary>
        /// <param name="image">The image metadata entity to insert.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task representing the asynchronous insert operation.</returns>
        public async Task SeedImageMetadataOnlyAsync(ImageEntity image, CancellationToken cancellationToken)
        {
                await using TestContentDbContext context = this._fixture.CreateDbContext();

                // Intentionally omit ImageBinaries so stream-retrieval tests can reproduce an orphaned metadata record.
                context.Images.Add(image);
                await context.SaveChangesAsync(cancellationToken: cancellationToken);
        }
}

