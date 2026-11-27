using ContentService.Application.Common;
using ContentService.Application.Interfaces;
using ContentService.Domain.Common;
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
                string? albumPath,
                bool includeSubalbums,
                CancellationToken cancellationToken)
        {
                // Exclude soft-deleted images before counting so TotalCount reflects only active catalog entries.
                IQueryable<ImageEntity> query = this._dbContext.Images
                        .AsNoTracking()
                        .Where(image => !image.IsDeleted);

                query = ImageCatalogQueryFilter.ApplyAlbumFilter(
                        query: query,
                        albumPath: albumPath,
                        includeSubalbums: includeSubalbums);

                query = ImageCatalogQueryFilter.ApplySearchFilter(
                        query: query,
                        searchTerm: searchTerm);

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
        public async Task<IReadOnlyList<string>> ListChildAlbumPathsAsync(
                string? parentAlbumPath,
                CancellationToken cancellationToken)
        {
                IQueryable<string> albumQuery = this._dbContext.Images
                        .AsNoTracking()
                        .Where(image => !image.IsDeleted && image.AlbumPath != null)
                        .Select(image => image.AlbumPath!);

                if (parentAlbumPath is not null)
                {
                        string prefix = $"{parentAlbumPath}{DomainConstants.ALBUM_PATH_SEPARATOR}";
                        albumQuery = albumQuery.Where(path => path.StartsWith(prefix));
                }

                List<string> distinctDescendantPaths = await albumQuery
                        .Distinct()
                        .ToListAsync(cancellationToken: cancellationToken);

                return ImageCatalogQueryFilter.ExtractImmediateChildAlbums(
                        parentAlbumPath: parentAlbumPath,
                        descendantPaths: distinctDescendantPaths);
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

        /// <inheritdoc />
        public async Task UpdateMetadataAsync(
                ImageEntity image,
                Guid expectedConcurrencyStamp,
                CancellationToken cancellationToken)
        {
                ArgumentNullException.ThrowIfNull(argument: image);

                // Non-standard optimistic concurrency: Uses a single-statement ExecuteUpdateAsync predicate instead of
                // loading a tracked entity and calling SaveChangesAsync.
                // Gain: Atomic check-and-update in one SQL round-trip without tracking overhead.
                // Loss: Bypasses EF Core ChangeTracker interceptors/events and requires a follow-up AnyAsync on 0 rows to distinguish 404 from 409.
                int rowsAffected = await this._dbContext.Images
                        .Where(existing => existing.Id == image.Id
                                && !existing.IsDeleted
                                && existing.ConcurrencyStamp == expectedConcurrencyStamp)
                        .ExecuteUpdateAsync(
                                setPropertyCalls: setters => setters
                                        .SetProperty(propertyExpression: existing => existing.AltText, valueExpression: image.AltText)
                                        .SetProperty(propertyExpression: existing => existing.Tags, valueExpression: image.Tags)
                                        .SetProperty(propertyExpression: existing => existing.AlbumPath, valueExpression: image.AlbumPath)
                                        .SetProperty(propertyExpression: existing => existing.UpdatedAt, valueExpression: image.UpdatedAt)
                                        .SetProperty(propertyExpression: existing => existing.UpdatedBy, valueExpression: image.UpdatedBy)
                                        .SetProperty(propertyExpression: existing => existing.ConcurrencyStamp, valueExpression: image.ConcurrencyStamp),
                                cancellationToken: cancellationToken);

                if (rowsAffected > 0)
                {
                        return;
                }

                // Distinguish deleted/missing images (404) from stale concurrency stamps (409) when zero rows matched.
                bool stillExists = await this._dbContext.Images
                        .AsNoTracking()
                        .AnyAsync(
                                predicate: existing => existing.Id == image.Id && !existing.IsDeleted,
                                cancellationToken: cancellationToken);

                if (!stillExists)
                {
                        throw new KeyNotFoundException($"Image '{image.Id}' was not found.");
                }

                throw new ConcurrencyConflictException($"Image '{image.Id}' was modified concurrently.");
        }
}
