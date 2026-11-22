using ContentService.Application.Interfaces;
using ContentService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ContentService.Infrastructure.Repositories;

/// <summary>
/// Loads stored image byte payloads from SQL Server separately from metadata queries.
/// </summary>
public sealed class SqlServerImageBinaryStore : IImageBinaryStore
{
        /// <summary>
        /// Scoped EF Core database context used to read image binary rows.
        /// </summary>
        private readonly ContentDbContext _dbContext;

        /// <summary>
        /// Captures the request-scoped <see cref="ContentDbContext"/> so binary reads share the active database configuration.
        /// </summary>
        /// <param name="dbContext">The scoped ContentService EF Core database context.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="dbContext"/> is <see langword="null"/>.</exception>
        public SqlServerImageBinaryStore(ContentDbContext dbContext)
        {
                ArgumentNullException.ThrowIfNull(dbContext);
                this._dbContext = dbContext;
        }

        /// <inheritdoc />
        public async Task<Stream?> OpenReadAsync(Guid imageId, CancellationToken cancellationToken)
        {
                // Join with Images so soft-deleted images cannot be downloaded, and select only the byte[] payload with AsNoTracking.
                byte[]? payloadBytes = await (
                        from binary in this._dbContext.ImageBinaries.AsNoTracking()
                        join image in this._dbContext.Images.AsNoTracking()
                                on binary.ImageId equals image.Id
                        where binary.ImageId == imageId && !image.IsDeleted
                        select binary.Data)
                        .FirstOrDefaultAsync(cancellationToken: cancellationToken);

                // Return a read-only MemoryStream so callers can dispose it normally without keeping SQL connections open during HTTP writes.
                return payloadBytes is null
                        ? null
                        : new MemoryStream(buffer: payloadBytes, writable: false);
        }
}
