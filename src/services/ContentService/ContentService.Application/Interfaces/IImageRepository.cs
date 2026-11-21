using ContentService.Application.Common;
using ContentService.Domain.Entities;

namespace ContentService.Application.Interfaces;

/// <summary>
/// Defines persistence operations for <see cref="ImageEntity"/> metadata and atomic creation with <see cref="ImageBinaryEntity"/> payloads.
/// </summary>
/// <remarks>
/// Infrastructure implementations encapsulate EF Core change tracking and SQL Server transactions behind this port.
/// </remarks>
public interface IImageRepository
{
        /// <summary>
        /// Retrieves image metadata by unique identifier, or <see langword="null"/> if not found.
        /// </summary>
        /// <param name="id">The unique image identifier.</param>
        /// <param name="cancellationToken">A token to observe while awaiting the lookup.</param>
        /// <returns>The matching <see cref="ImageEntity"/>, or <see langword="null"/> when absent.</returns>
        public Task<ImageEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

        /// <summary>
        /// Batch-loads image metadata entries matching the supplied identifiers for template verification and publish packaging.
        /// </summary>
        /// <param name="ids">The collection of image identifiers to fetch.</param>
        /// <param name="cancellationToken">A token to observe while awaiting the query.</param>
        /// <returns>A read-only list containing whichever matching <see cref="ImageEntity"/> records exist.</returns>
        public Task<IReadOnlyList<ImageEntity>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

        /// <summary>
        /// Retrieves an active image metadata record matching a lowercase hexadecimal SHA-256 content digest, if present.
        /// </summary>
        /// <param name="sha256Hash">The 64-character lowercase hexadecimal SHA-256 digest.</param>
        /// <param name="cancellationToken">A token to observe while awaiting the lookup.</param>
        /// <returns>The matching <see cref="ImageEntity"/>, or <see langword="null"/> when no matching active image exists.</returns>
        public Task<ImageEntity?> GetBySha256Async(string sha256Hash, CancellationToken cancellationToken);

        /// <summary>
        /// Lists a single page of active (non-deleted) images optionally filtered by file name search text.
        /// </summary>
        /// <param name="page">The clamped 1-based page index.</param>
        /// <param name="pageSize">The clamped page size.</param>
        /// <param name="searchTerm">Optional case-insensitive file name search filter.</param>
        /// <param name="cancellationToken">A token to observe while awaiting the query.</param>
        /// <returns>A <see cref="PagedResult{T}"/> containing active matching images.</returns>
        public Task<PagedResult<ImageEntity>> ListAsync(int page, int pageSize, string? searchTerm, CancellationToken cancellationToken);

        /// <summary>
        /// Determines whether any template—including archived templates—currently references the specified image.
        /// </summary>
        /// <remarks>
        /// Archived templates remain publishable for active drafts in <c>EditorService</c>, so implementations must count both active and archived template references.
        /// </remarks>
        /// <param name="imageId">The identifier of the image to check.</param>
        /// <param name="cancellationToken">A token to observe while awaiting the reference check.</param>
        /// <returns><see langword="true"/> if at least one template references <paramref name="imageId"/>; otherwise <see langword="false"/>.</returns>
        public Task<bool> IsReferencedByAnyTemplateAsync(Guid imageId, CancellationToken cancellationToken);

        /// <summary>
        /// Persists image metadata and its binary payload together as a single atomic unit of work.
        /// </summary>
        /// <param name="metadata">The validated image metadata entity.</param>
        /// <param name="binary">The validated binary payload entity sharing <paramref name="metadata"/>'s identity.</param>
        /// <param name="cancellationToken">A token to observe while committing the transaction.</param>
        /// <returns>A task representing the asynchronous persistence operation.</returns>
        public Task AddAsync(ImageEntity metadata, ImageBinaryEntity binary, CancellationToken cancellationToken);

        /// <summary>
        /// Persists the soft-deleted state of an <see cref="ImageEntity"/> while verifying optimistic concurrency.
        /// </summary>
        /// <remarks>
        /// Implementations must preserve the row with <see cref="ImageEntity.IsDeleted"/> set to <see langword="true"/> rather than physically deleting it, and throw <see cref="Domain.Exceptions.ConcurrencyConflictException"/> if <paramref name="expectedConcurrencyStamp"/> is stale.
        /// </remarks>
        /// <param name="image">The soft-deleted image entity.</param>
        /// <param name="expectedConcurrencyStamp">The concurrency stamp observed before <see cref="ImageEntity.MarkDeleted"/> rotated it.</param>
        /// <param name="cancellationToken">A token to observe while persisting the update.</param>
        /// <returns>A task representing the asynchronous soft-delete operation.</returns>
        /// <exception cref="Domain.Exceptions.ConcurrencyConflictException">Thrown when <paramref name="expectedConcurrencyStamp"/> does not match the persisted stamp.</exception>
        public Task DeleteAsync(ImageEntity image, Guid expectedConcurrencyStamp, CancellationToken cancellationToken);
}

