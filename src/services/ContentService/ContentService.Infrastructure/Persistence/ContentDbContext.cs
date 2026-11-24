using ContentService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ContentService.Infrastructure.Persistence;

/// <summary>
/// Provides the EF Core unit-of-work and query entry point for ContentService aggregates while keeping table mapping isolated in dedicated configuration classes.
/// </summary>
public sealed class ContentDbContext : DbContext
{
        /// <summary>
        /// Accepts configured provider options so the same context works against production SQL Server and Testcontainers integration databases.
        /// </summary>
        /// <param name="options">The configured SQL Server DbContext options.</param>
        public ContentDbContext(DbContextOptions<ContentDbContext> options)
                : base(options)
        {
        }

        /// <summary>
        /// Queryable metadata set kept separate from <see cref="ImageBinaries"/> so catalog listings and quota sums never touch heavy <c>varbinary(max)</c> columns.
        /// </summary>
        public DbSet<ImageEntity> Images => this.Set<ImageEntity>();

        /// <summary>
        /// Isolated binary storage set enabling sequential streaming and metadata-free payload storage.
        /// </summary>
        public DbSet<ImageBinaryEntity> ImageBinaries => this.Set<ImageBinaryEntity>();

        /// <summary>
        /// Queryable exhibition template set used for layout management and offline bundle resolution.
        /// </summary>
        public DbSet<TemplateEntity> Templates => this.Set<TemplateEntity>();

        /// <summary>
        /// Indexed link table set enabling fast referential-integrity checks before soft-deleting images.
        /// </summary>
        public DbSet<TemplateImageReference> TemplateImageReferences => this.Set<TemplateImageReference>();

        /// <summary>
        /// Delegates schema configuration to per-entity <see cref="IEntityTypeConfiguration{TEntity}"/> classes so mapping rules stay modular and maintainable as tables evolve.
        /// </summary>
        /// <param name="modelBuilder">The EF Core model builder.</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
                ArgumentNullException.ThrowIfNull(modelBuilder);

                // Auto-discover per-entity configurations in this assembly so new tables cannot accidentally be left unconfigured.
                modelBuilder.ApplyConfigurationsFromAssembly(typeof(ContentDbContext).Assembly);
        }
}
