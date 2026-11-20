using ContentService.Application.Common;
using ContentService.Domain.Entities;
using ContentService.Domain.Enums;

namespace ContentService.Application.Interfaces;

/// <summary>
/// Defines persistence operations for versioned <see cref="TemplateEntity"/> aggregates and their indexed image references.
/// </summary>
public interface ITemplateRepository
{
        /// <summary>
        /// Retrieves a template aggregate—including its <see cref="TemplateEntity.ReferencedImages"/> links—by identifier.
        /// </summary>
        /// <param name="id">The unique template identifier.</param>
        /// <param name="cancellationToken">A token to observe while awaiting the lookup.</param>
        /// <returns>The matching <see cref="TemplateEntity"/>, or <see langword="null"/> when absent.</returns>
        public Task<TemplateEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

        /// <summary>
        /// Determines whether a template with the specified display name already exists, optionally excluding a template being updated.
        /// </summary>
        /// <param name="name">The trimmed template display name to check.</param>
        /// <param name="excludeTemplateId">Optional template ID excluded when verifying uniqueness during updates.</param>
        /// <param name="cancellationToken">A token to observe while awaiting the check.</param>
        /// <returns><see langword="true"/> if another template uses <paramref name="name"/>; otherwise <see langword="false"/>.</returns>
        public Task<bool> ExistsByNameAsync(string name, Guid? excludeTemplateId, CancellationToken cancellationToken);

        /// <summary>
        /// Lists a single page of templates optionally filtered by component type and display name search text.
        /// </summary>
        /// <param name="page">The clamped 1-based page index.</param>
        /// <param name="pageSize">The clamped page size.</param>
        /// <param name="componentType">Optional UI component category filter.</param>
        /// <param name="searchTerm">Optional case-insensitive template name filter.</param>
        /// <param name="cancellationToken">A token to observe while awaiting the query.</param>
        /// <returns>A <see cref="PagedResult{T}"/> containing matching templates.</returns>
        public Task<PagedResult<TemplateEntity>> ListAsync(
                int page,
                int pageSize,
                TemplateComponentType? componentType,
                string? searchTerm,
                CancellationToken cancellationToken);

        /// <summary>
        /// Persists a newly created template aggregate and its indexed image reference links.
        /// </summary>
        /// <param name="template">The validated template entity to insert.</param>
        /// <param name="cancellationToken">A token to observe while persisting the template.</param>
        /// <returns>A task representing the asynchronous persistence operation.</returns>
        public Task AddAsync(TemplateEntity template, CancellationToken cancellationToken);

        /// <summary>
        /// Persists schema and image-reference updates for an existing template while checking optimistic concurrency.
        /// </summary>
        /// <remarks>
        /// Infrastructure implementations must translate concurrency stamp mismatches into <see cref="Domain.Exceptions.ConcurrencyConflictException"/>.
        /// </remarks>
        /// <param name="template">The mutated template entity with updated schema and rotated <see cref="Domain.Common.BaseEntity.ConcurrencyStamp"/>.</param>
        /// <param name="expectedConcurrencyStamp">The concurrency stamp observed before <see cref="TemplateEntity.UpdateSchema"/> rotated it.</param>
        /// <param name="cancellationToken">A token to observe while persisting the update.</param>
        /// <returns>A task representing the asynchronous update operation.</returns>
        /// <exception cref="Domain.Exceptions.ConcurrencyConflictException">Thrown when <paramref name="expectedConcurrencyStamp"/> does not match the persisted stamp.</exception>
        public Task UpdateAsync(TemplateEntity template, Guid expectedConcurrencyStamp, CancellationToken cancellationToken);

        /// <summary>
        /// Persists the archived state of a <see cref="TemplateEntity"/> while checking optimistic concurrency.
        /// </summary>
        /// <remarks>
        /// Implementations must retain the archived row so active drafts remain resolvable, and throw <see cref="Domain.Exceptions.ConcurrencyConflictException"/> if <paramref name="expectedConcurrencyStamp"/> is stale.
        /// </remarks>
        /// <param name="template">The archived template entity.</param>
        /// <param name="expectedConcurrencyStamp">The concurrency stamp observed before <see cref="TemplateEntity.Archive"/> rotated it.</param>
        /// <param name="cancellationToken">A token to observe while persisting the archive operation.</param>
        /// <returns>A task representing the asynchronous archive operation.</returns>
        /// <exception cref="Domain.Exceptions.ConcurrencyConflictException">Thrown when <paramref name="expectedConcurrencyStamp"/> does not match the persisted stamp.</exception>
        public Task DeleteAsync(TemplateEntity template, Guid expectedConcurrencyStamp, CancellationToken cancellationToken);
}

