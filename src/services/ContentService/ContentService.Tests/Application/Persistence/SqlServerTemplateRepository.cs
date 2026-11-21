using ContentService.Application.Interfaces;
using ContentService.Application.Common;
using ContentService.Domain.Entities;
using ContentService.Domain.Enums;
using ContentService.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ContentService.Tests.Application.Persistence;

/// <summary>
/// SQL Server EF Core implementation of <see cref="ITemplateRepository"/> executed against a real Testcontainers MSSQL instance.
/// </summary>
public sealed class SqlServerTemplateRepository : ITemplateRepository
{
        /// <summary>
        /// Database fixture providing fresh <see cref="TestContentDbContext"/> instances.
        /// </summary>
        private readonly MsSqlContentDatabaseFixture _fixture;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerTemplateRepository"/> class.
        /// </summary>
        /// <param name="fixture">The shared SQL Server test container fixture.</param>
        public SqlServerTemplateRepository(MsSqlContentDatabaseFixture fixture)
        {
                ArgumentNullException.ThrowIfNull(fixture);
                this._fixture = fixture;
        }

        /// <inheritdoc />
        public async Task<TemplateEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
                await using TestContentDbContext context = this._fixture.CreateDbContext();

                // Eager-load ReferencedImages with AsNoTracking so callers receive a detached aggregate with populated references.
                return await context.Templates
                        .AsNoTracking()
                        .Include(template => template.ReferencedImages)
                        .FirstOrDefaultAsync(template => template.Id == id, cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public async Task<bool> ExistsByNameAsync(string name, Guid? excludeTemplateId, CancellationToken cancellationToken)
        {
                await using TestContentDbContext context = this._fixture.CreateDbContext();

                // Normalize name before SQL comparison and optionally exclude the template being updated.
                string normalizedName = name.Trim().ToLowerInvariant();
                IQueryable<TemplateEntity> query = context.Templates
                        .AsNoTracking()
                        .Where(template => template.Name.ToLower() == normalizedName);

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
                await using TestContentDbContext context = this._fixture.CreateDbContext();
                // Eager-load ReferencedImages so ContentResultMapper can project ReferencedPictureReferences.
                IQueryable<TemplateEntity> query = context.Templates
                        .AsNoTracking()
                        .Include(template => template.ReferencedImages);

                if (componentType.HasValue)
                {
                        query = query.Where(template => template.ComponentType == componentType.Value);
                }

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                        // Use ToLower().Contains(...) rather than StringComparison.OrdinalIgnoreCase so EF Core translates it to SQL.
                        string normalizedSearch = searchTerm.Trim().ToLowerInvariant();
                        query = query.Where(template => template.Name.ToLower().Contains(normalizedSearch));
                }

                int totalCount = await query.CountAsync(cancellationToken: cancellationToken);

                // Compute skip count in 64-bit arithmetic and clamp to int.MaxValue so extreme page numbers never overflow.
                int skipCount = (int)Math.Min(int.MaxValue, (long)(page - 1) * pageSize);

                // Order by CreatedAt then Id as a deterministic tie-breaker when multiple templates share the same timestamp.
                List<TemplateEntity> items = await query
                        .OrderBy(template => template.CreatedAt)
                        .ThenBy(template => template.Id)
                        .Skip(skipCount)
                        .Take(pageSize)
                        .ToListAsync(cancellationToken: cancellationToken);

                return new PagedResult<TemplateEntity>
                {
                        Items = items,
                        Page = page,
                        PageSize = pageSize,
                        TotalCount = totalCount
                };
        }

        /// <inheritdoc />
        public async Task AddAsync(TemplateEntity template, CancellationToken cancellationToken)
        {
                await using TestContentDbContext context = this._fixture.CreateDbContext();

                // Persist the template row and its owned TemplateImageReference join rows in one SaveChangesAsync unit of work.
                context.Templates.Add(template);
                await context.SaveChangesAsync(cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public async Task UpdateAsync(TemplateEntity template, Guid expectedConcurrencyStamp, CancellationToken cancellationToken)
        {
                await using TestContentDbContext context = this._fixture.CreateDbContext();
                await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancellationToken: cancellationToken);

                // Guard the update with WHERE ConcurrencyStamp = @expectedConcurrencyStamp inside an atomic SQL transaction.
                int rowsAffected = await context.Templates
                        .Where(existing => existing.Id == template.Id && existing.ConcurrencyStamp == expectedConcurrencyStamp)
                        .ExecuteUpdateAsync(
                                setters => setters
                                        .SetProperty(existing => existing.Name, template.Name)
                                        .SetProperty(existing => existing.SchemaJson, template.SchemaJson)
                                        .SetProperty(existing => existing.SchemaVersion, template.SchemaVersion)
                                        .SetProperty(existing => existing.UpdatedAt, template.UpdatedAt)
                                        .SetProperty(existing => existing.UpdatedBy, template.UpdatedBy)
                                        .SetProperty(existing => existing.ConcurrencyStamp, template.ConcurrencyStamp),
                                cancellationToken: cancellationToken);

                // Throwing rolls back the ambient transaction automatically on await using disposal.
                if (rowsAffected == 0)
                {
                        throw new ConcurrencyConflictException($"Template '{template.Id}' was modified concurrently.");
                }

                // Replace indexed TemplateImageReference rows via delete-and-reinsert to avoid EF Core change-tracking conflicts on detached entities.
                await context.TemplateImageReferences
                        .Where(reference => reference.TemplateId == template.Id)
                        .ExecuteDeleteAsync(cancellationToken: cancellationToken);

                if (template.ReferencedImages.Count > 0)
                {
                        context.TemplateImageReferences.AddRange(template.ReferencedImages);
                        await context.SaveChangesAsync(cancellationToken: cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public async Task DeleteAsync(TemplateEntity template, Guid expectedConcurrencyStamp, CancellationToken cancellationToken)
        {
                await using TestContentDbContext context = this._fixture.CreateDbContext();

                // Archive atomically with the concurrency stamp predicate so stale deletes fail without overwriting newer edits.
                int rowsAffected = await context.Templates
                        .Where(existing => existing.Id == template.Id && existing.ConcurrencyStamp == expectedConcurrencyStamp)
                        .ExecuteUpdateAsync(
                                setters => setters
                                        .SetProperty(existing => existing.IsArchived, template.IsArchived)
                                        .SetProperty(existing => existing.UpdatedAt, template.UpdatedAt)
                                        .SetProperty(existing => existing.UpdatedBy, template.UpdatedBy)
                                        .SetProperty(existing => existing.ConcurrencyStamp, template.ConcurrencyStamp),
                                cancellationToken: cancellationToken);

                if (rowsAffected == 0)
                {
                        throw new ConcurrencyConflictException($"Template '{template.Id}' was modified concurrently.");
                }
        }

        /// <summary>
        /// Directly rotates the persisted <see cref="TemplateEntity.ConcurrencyStamp"/> in SQL Server to simulate a concurrent writer.
        /// </summary>
        /// <param name="templateId">The target template identifier.</param>
        /// <param name="newConcurrencyStamp">The replacement concurrency stamp.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task representing the asynchronous SQL update.</returns>
        public async Task RotatePersistedConcurrencyStampAsync(
                Guid templateId,
                Guid newConcurrencyStamp,
                CancellationToken cancellationToken)
        {
                await using TestContentDbContext context = this._fixture.CreateDbContext();

                // Update only the concurrency stamp directly in SQL to mimic another request committing between read and write.
                await context.Templates
                        .Where(template => template.Id == templateId)
                        .ExecuteUpdateAsync(
                                setters => setters.SetProperty(template => template.ConcurrencyStamp, newConcurrencyStamp),
                                cancellationToken: cancellationToken);
        }
}

