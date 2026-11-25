using ContentService.Application.Common;
using ContentService.Application.Interfaces;
using ContentService.Domain.Entities;
using ContentService.Domain.Exceptions;
using ContentService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ContentService.Infrastructure.Repositories;

/// <summary>
/// Persists and queries <see cref="ImageEntity"/> metadata in SQL Server without loading binary payloads or keeping stale EF Core tracked entities.
/// </summary>
public sealed class SqlServerImageRepository : IImageRepository
{
        /// <summary>
        /// Scoped EF Core database context used for metadata queries and atomic image/binary inserts.
        /// </summary>
        private readonly ContentDbContext _dbContext;

        /// <summary>
        /// Captures the request-scoped <see cref="ContentDbContext"/> so image metadata operations share the active DI scope.
        /// </summary>
        /// <param name="dbContext">The scoped ContentService EF Core database context.</param>
        public SqlServerImageRepository(ContentDbContext dbContext)
        {
                ArgumentNullException.ThrowIfNull(dbContext);
                this._dbContext = dbContext;
        }

        /// <inheritdoc />
        public async Task<ImageEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
                return await this._dbContext.Images
                        .AsNoTracking()
                        .FirstOrDefaultAsync(predicate: image => image.Id == id, cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ImageEntity>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
        {
                ArgumentNullException.ThrowIfNull(ids);

                // Short-circuit empty batches to avoid unnecessary SQL round-trips when no images are referenced.
                if (ids.Count == 0)
                {
                        return [];
                }

                return await this._dbContext.Images
                        .AsNoTracking()
                        .Where(image => ids.Contains(image.Id))
                        .ToListAsync(cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public async Task<ImageEntity?> GetBySha256Async(string sha256Hash, CancellationToken cancellationToken)
        {
                // Exclude soft-deleted rows so deleted assets are never resurrected or matched during deduplication checks.
                return await this._dbContext.Images
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                                predicate: image => !image.IsDeleted && image.Sha256Hash == sha256Hash,
                                cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public async Task<PagedResult<ImageEntity>> ListAsync(
                int page,
                int pageSize,
                string? searchTerm,
                CancellationToken cancellationToken)
        {
                // Exclude soft-deleted images before counting so TotalCount reflects only active catalog entries.
                IQueryable<ImageEntity> query = this._dbContext.Images
                        .AsNoTracking()
                        .Where(image => !image.IsDeleted);

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                        // EF Core cannot translate StringComparison.OrdinalIgnoreCase or ToLowerInvariant() to SQL, so we normalize the input in C# and call .ToLower() in LINQ.
                        string normalizedSearch = searchTerm.Trim().ToLowerInvariant();
                        query = query.Where(image => image.FileName.ToLower().Contains(normalizedSearch));
                }

                int totalCount = await query.CountAsync(cancellationToken: cancellationToken);
                (int safePage, int safePageSize, int skipCount) = PaginationNormalizer.Normalize(
                        page: page,
                        pageSize: pageSize);

                // Break CreatedAt ties with Id so pagination order stays deterministic across rows inserted within the same clock tick.
                List<ImageEntity> items = await query
                        .OrderBy(image => image.CreatedAt)
                        .ThenBy(image => image.Id)
                        .Skip(skipCount)
                        .Take(safePageSize)
                        .ToListAsync(cancellationToken: cancellationToken);

                return new PagedResult<ImageEntity>
                {
                        Items = items,
                        Page = safePage,
                        PageSize = safePageSize,
                        TotalCount = totalCount
                };
        }

        /// <inheritdoc />
        public async Task<bool> IsReferencedByAnyTemplateAsync(Guid imageId, CancellationToken cancellationToken)
        {
                // Check all persisted templates including archived ones so active exhibition drafts referencing an archived template never lose their images.
                return await this._dbContext.TemplateImageReferences
                        .AsNoTracking()
                        .AnyAsync(predicate: reference => reference.ImageId == imageId, cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public async Task AddAsync(ImageEntity metadata, ImageBinaryEntity binary, CancellationToken cancellationToken)
        {
                ArgumentNullException.ThrowIfNull(metadata);
                ArgumentNullException.ThrowIfNull(binary);

                // Save metadata and binary rows together in one SaveChangesAsync transaction so partial uploads never leave metadata without bytes.
                this._dbContext.Images.Add(metadata);
                this._dbContext.ImageBinaries.Add(binary);

                try
                {
                        await this._dbContext.SaveChangesAsync(cancellationToken: cancellationToken);
                }
                finally
                {
                        // Clear ChangeTracker so the multi-megabyte byte[] in ImageBinaryEntity can be garbage-collected and cannot collide with later operations in this HTTP request.
                        this._dbContext.ChangeTracker.Clear();
                }
        }

        /// <inheritdoc />
        public async Task DeleteAsync(ImageEntity image, Guid expectedConcurrencyStamp, CancellationToken cancellationToken)
        {
                ArgumentNullException.ThrowIfNull(image);

                // Soft-delete in SQL only if ConcurrencyStamp (the optimistic edit-lock token) still matches what the caller loaded.
                int rowsAffected = await this._dbContext.Images
                        .Where(existing => existing.Id == image.Id && existing.ConcurrencyStamp == expectedConcurrencyStamp)
                        .ExecuteUpdateAsync(
                                setPropertyCalls: setters => setters
                                        .SetProperty(propertyExpression: existing => existing.IsDeleted, valueExpression: image.IsDeleted)
                                        .SetProperty(propertyExpression: existing => existing.UpdatedAt, valueExpression: image.UpdatedAt)
                                        .SetProperty(propertyExpression: existing => existing.UpdatedBy, valueExpression: image.UpdatedBy)
                                        .SetProperty(propertyExpression: existing => existing.ConcurrencyStamp, valueExpression: image.ConcurrencyStamp),
                                cancellationToken: cancellationToken);

                // Zero rows updated means another request deleted or modified the image after the caller read its ConcurrencyStamp.
                if (rowsAffected == 0)
                {
                        throw new ConcurrencyConflictException($"Image '{image.Id}' was modified concurrently.");
                }
        }
}
