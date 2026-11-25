using ContentService.Application.Common;
using ContentService.Application.Interfaces;
using ContentService.Domain.Entities;
using ContentService.Domain.Enums;
using ContentService.Domain.Exceptions;
using ContentService.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ContentService.Infrastructure.Repositories;

/// <summary>
/// Persists and queries <see cref="TemplateEntity"/> aggregates in SQL Server using split queries, optimistic edit-conflict checks, and transactional link updates.
/// </summary>
public sealed class SqlServerTemplateRepository : ITemplateRepository
{
        /// <summary>
        /// Scoped EF Core database context used for template queries and transactional updates.
        /// </summary>
        private readonly ContentDbContext _dbContext;

        /// <summary>
        /// Captures the request-scoped <see cref="ContentDbContext"/> so template operations share the active DI scope.
        /// </summary>
        /// <param name="dbContext">The scoped ContentService EF Core database context.</param>
        public SqlServerTemplateRepository(ContentDbContext dbContext)
        {
                ArgumentNullException.ThrowIfNull(dbContext);
                this._dbContext = dbContext;
        }

        /// <inheritdoc />
        public async Task<TemplateEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
                // Load ReferencedImages together with the template using AsNoTracking since callers only read here.
                return await this._dbContext.Templates
                        .AsNoTracking()
                        .Include(template => template.ReferencedImages)
                        .FirstOrDefaultAsync(predicate: template => template.Id == id, cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public async Task<bool> ExistsByNameAsync(string name, Guid? excludeTemplateId, CancellationToken cancellationToken)
        {
                ArgumentException.ThrowIfNullOrWhiteSpace(name);

                // Normalize the C# search value with ToLowerInvariant() (culture-safe, unlike ToLower() on Turkish 'I'), while SQL uses .ToLower() because EF Core cannot translate ToLowerInvariant().
                string normalizedName = name.Trim().ToLowerInvariant();
                IQueryable<TemplateEntity> query = this._dbContext.Templates
                        .AsNoTracking()
                        .Where(template => template.Name.ToLower() == normalizedName);

                // Exclude the current template ID during updates so keeping the same name does not collide with itself.
                if (excludeTemplateId.HasValue)
                {
                        query = query.Where(template => template.Id != excludeTemplateId.Value);
                }

                return await query.AnyAsync(cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public async Task<PagedResult<TemplateEntity>> ListAsync(
                int page,
                int pageSize,
                TemplateComponentType? componentType,
                string? searchTerm,
                CancellationToken cancellationToken)
        {
                // AsSplitQuery runs two separate SQL queries (templates + image links) so SQL does not repeat the large SchemaJson string on every linked image row (paging happens below via Skip/Take).
                IQueryable<TemplateEntity> query = this._dbContext.Templates
                        .AsNoTracking()
                        .Include(template => template.ReferencedImages)
                        .AsSplitQuery();

                if (componentType.HasValue)
                {
                        query = query.Where(template => template.ComponentType == componentType.Value);
                }

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                        // EF Core cannot translate StringComparison.OrdinalIgnoreCase or ToLowerInvariant() to SQL, so we normalize the input in C# and call .ToLower() in LINQ.
                        string normalizedSearch = searchTerm.Trim().ToLowerInvariant();
                        query = query.Where(template => template.Name.ToLower().Contains(normalizedSearch));
                }

                int totalCount = await query.CountAsync(cancellationToken: cancellationToken);
                (int safePage, int safePageSize, int skipCount) = PaginationNormalizer.Normalize(
                        page: page,
                        pageSize: pageSize);

                // Break CreatedAt ties with Id so paging order never shifts when two rows share the same timestamp.
                List<TemplateEntity> items = await query
                        .OrderBy(template => template.CreatedAt)
                        .ThenBy(template => template.Id)
                        .Skip(skipCount)
                        .Take(safePageSize)
                        .ToListAsync(cancellationToken: cancellationToken);

                return new PagedResult<TemplateEntity>
                {
                        Items = items,
                        Page = safePage,
                        PageSize = safePageSize,
                        TotalCount = totalCount
                };
        }

        /// <inheritdoc />
        public async Task AddAsync(TemplateEntity template, CancellationToken cancellationToken)
        {
                ArgumentNullException.ThrowIfNull(template);

                // Save the template and its TemplateImageReference join rows together so templates never exist without indexed image links.
                this._dbContext.Templates.Add(template);

                try
                {
                        await this._dbContext.SaveChangesAsync(cancellationToken: cancellationToken);
                }
                catch (DbUpdateException ex) when (IsSqlError(exception: ex, errorNumberA: PersistenceConstants.SQL_ERROR_DUPLICATE_INDEX_KEY, errorNumberB: PersistenceConstants.SQL_ERROR_UNIQUE_CONSTRAINT_VIOLATION))
                {
                        // Turn duplicate-name SQL race errors into a 400 validation error instead of a 500 server error.
                        throw new DomainValidationException(
                                message: $"A template named '{template.Name}' already exists.",
                                innerException: ex);
                }
                finally
                {
                        // Reset EF Core's in-memory tracking cache after writing so later operations in the same HTTP request do not read or collide with stale tracked objects.
                        this._dbContext.ChangeTracker.Clear();
                }
        }

        /// <inheritdoc />
        public async Task UpdateAsync(TemplateEntity template, Guid expectedConcurrencyStamp, CancellationToken cancellationToken)
        {
                ArgumentNullException.ThrowIfNull(template);

                // Wrap both SQL steps (update template columns + replace image links) in one all-or-nothing transaction so a failure in step 2 rolls back step 1.
                await using IDbContextTransaction transaction = await this._dbContext.Database.BeginTransactionAsync(
                        cancellationToken: cancellationToken);

                int rowsAffected = await this.UpdateTemplateColumnsWithConcurrencyCheckAsync(
                        template: template,
                        expectedConcurrencyStamp: expectedConcurrencyStamp,
                        cancellationToken: cancellationToken);

                // Zero updated rows means another user edited this template first (expectedConcurrencyStamp no longer matched in SQL).
                if (rowsAffected == 0)
                {
                        throw new ConcurrencyConflictException($"Template '{template.Id}' was modified concurrently.");
                }

                await this.ReplaceReferencedImagesAsync(template: template, cancellationToken: cancellationToken);
                await transaction.CommitAsync(cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public async Task DeleteAsync(TemplateEntity template, Guid expectedConcurrencyStamp, CancellationToken cancellationToken)
        {
                ArgumentNullException.ThrowIfNull(template);

                // Archive the template in SQL only if ConcurrencyStamp still matches, preventing a stale delete from overwriting a newer edit.
                int rowsAffected = await this._dbContext.Templates
                        .Where(existing => existing.Id == template.Id && existing.ConcurrencyStamp == expectedConcurrencyStamp)
                        .ExecuteUpdateAsync(
                                setPropertyCalls: setters => setters
                                        .SetProperty(propertyExpression: existing => existing.IsArchived, valueExpression: template.IsArchived)
                                        .SetProperty(propertyExpression: existing => existing.UpdatedAt, valueExpression: template.UpdatedAt)
                                        .SetProperty(propertyExpression: existing => existing.UpdatedBy, valueExpression: template.UpdatedBy)
                                        .SetProperty(propertyExpression: existing => existing.ConcurrencyStamp, valueExpression: template.ConcurrencyStamp),
                                cancellationToken: cancellationToken);

                if (rowsAffected == 0)
                {
                        throw new ConcurrencyConflictException($"Template '{template.Id}' was modified concurrently.");
                }
        }

        /// <summary>
        /// Updates template columns directly in SQL only if the optimistic edit-lock token (<paramref name="expectedConcurrencyStamp"/>) still matches.
        /// </summary>
        /// <param name="template">The updated template aggregate.</param>
        /// <param name="expectedConcurrencyStamp">The edit-lock token read before editing (unrelated to <c>async</c>/<c>await</c>).</param>
        /// <param name="cancellationToken">Propagates cancellation during SQL execution.</param>
        /// <returns>The number of updated template rows (<c>0</c> means missing template or another editor saved first).</returns>
        private async Task<int> UpdateTemplateColumnsWithConcurrencyCheckAsync(
                TemplateEntity template,
                Guid expectedConcurrencyStamp,
                CancellationToken cancellationToken)
        {
                try
                {
                        return await this._dbContext.Templates
                                .Where(existing => existing.Id == template.Id && existing.ConcurrencyStamp == expectedConcurrencyStamp)
                                .ExecuteUpdateAsync(
                                        setPropertyCalls: setters => setters
                                                .SetProperty(propertyExpression: existing => existing.Name, valueExpression: template.Name)
                                                .SetProperty(propertyExpression: existing => existing.SchemaJson, valueExpression: template.SchemaJson)
                                                .SetProperty(propertyExpression: existing => existing.SchemaVersion, valueExpression: template.SchemaVersion)
                                                .SetProperty(propertyExpression: existing => existing.UpdatedAt, valueExpression: template.UpdatedAt)
                                                .SetProperty(propertyExpression: existing => existing.UpdatedBy, valueExpression: template.UpdatedBy)
                                                .SetProperty(propertyExpression: existing => existing.ConcurrencyStamp, valueExpression: template.ConcurrencyStamp),
                                        cancellationToken: cancellationToken);
                }
                catch (SqlException ex) when (IsSqlUniqueConstraintError(ex))
                {
                        // ExecuteUpdateAsync bypasses SaveChangesAsync, so SQL unique violations arrive directly as SqlException rather than DbUpdateException.
                        throw new DomainValidationException(
                                message: $"A template named '{template.Name}' already exists.",
                                innerException: ex);
                }
        }

        /// <summary>
        /// Replaces <see cref="TemplateImageReference"/> link rows inside the active transaction by deleting the old links and inserting the new ones.
        /// </summary>
        /// <param name="template">The updated template aggregate containing target image links.</param>
        /// <param name="cancellationToken">Propagates cancellation during link replacement.</param>
        /// <returns>A task representing the asynchronous link replacement.</returns>
        private async Task ReplaceReferencedImagesAsync(TemplateEntity template, CancellationToken cancellationToken)
        {
                // Delete old link rows directly in SQL because the template was loaded with AsNoTracking (EF is not tracking differences on ReferencedImages).
                await this._dbContext.TemplateImageReferences
                        .Where(reference => reference.TemplateId == template.Id)
                        .ExecuteDeleteAsync(cancellationToken: cancellationToken);

                if (template.ReferencedImages.Count == 0)
                {
                        return;
                }

                this._dbContext.TemplateImageReferences.AddRange(template.ReferencedImages);

                try
                {
                        await this._dbContext.SaveChangesAsync(cancellationToken: cancellationToken);
                }
                catch (DbUpdateException ex) when (IsSqlError(exception: ex, errorNumberA: PersistenceConstants.SQL_ERROR_FOREIGN_KEY_CONFLICT))
                {
                        // Translate concurrent physical image deletion into a 400 validation error instead of an unhandled 500.
                        throw new DomainValidationException(
                                message: "One or more referenced images no longer exist.",
                                innerException: ex);
                }
                finally
                {
                        // Clear EF Core's in-memory tracking cache after AddRange/SaveChangesAsync so later queries in this HTTP request do not reuse stale tracked join entities.
                        this._dbContext.ChangeTracker.Clear();
                }
        }

        /// <summary>
        /// Checks whether a <see cref="DbUpdateException"/> wraps one of the given SQL Server error codes.
        /// </summary>
        /// <param name="exception">The database update exception.</param>
        /// <param name="errorNumberA">First SQL Server error number to match.</param>
        /// <param name="errorNumberB">Optional second SQL Server error number to match.</param>
        /// <returns><see langword="true"/> when the inner <see cref="SqlException"/> matches; otherwise <see langword="false"/>.</returns>
        private static bool IsSqlError(DbUpdateException exception, int errorNumberA, int? errorNumberB = null) =>
                exception.InnerException is SqlException sqlException
                && (sqlException.Number == errorNumberA
                        || (errorNumberB.HasValue && sqlException.Number == errorNumberB.Value));

        /// <summary>
        /// Matches SQL Server error codes 2601 and 2627 instead of parsing localized error text so duplicate-name detection works on every SQL language setting.
        /// </summary>
        /// <param name="sqlException">The SQL Server exception.</param>
        /// <returns><see langword="true"/> for duplicate key or unique constraint errors; otherwise <see langword="false"/>.</returns>
        private static bool IsSqlUniqueConstraintError(SqlException sqlException) =>
                sqlException.Number == PersistenceConstants.SQL_ERROR_DUPLICATE_INDEX_KEY
                || sqlException.Number == PersistenceConstants.SQL_ERROR_UNIQUE_CONSTRAINT_VIOLATION;
}
