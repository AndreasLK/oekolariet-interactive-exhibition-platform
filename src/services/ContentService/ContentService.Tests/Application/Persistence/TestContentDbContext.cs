using ContentService.Domain.Common;
using ContentService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ContentService.Tests.Application.Persistence;

/// <summary>
/// Test-scoped Entity Framework Core <see cref="DbContext"/> mapping ContentService domain aggregates to SQL Server tables.
/// </summary>
/// <remarks>
/// Lives inside <c>ContentService.Tests</c> so <c>ContentService.Application</c> stays 100% persistence-ignorant while still verifying real MSSQL behavior.
/// </remarks>
public sealed class TestContentDbContext : DbContext
{
        /// <summary>
        /// Maximum column width for normalized MIME content types.
        /// </summary>
        private const int MAX_CONTENT_TYPE_LENGTH = 64;

        /// <summary>
        /// Backing field name on <see cref="TemplateEntity"/> storing <see cref="TemplateImageReference"/> join rows.
        /// </summary>
        private const string REFERENCED_IMAGES_FIELD_NAME = "_referencedImages";

        /// <summary>
        /// SQL Server table name storing <see cref="ImageEntity"/> metadata records.
        /// </summary>
        private const string IMAGES_TABLE_NAME = "Images";

        /// <summary>
        /// SQL Server table name storing <see cref="ImageBinaryEntity"/> raw binary payloads.
        /// </summary>
        private const string IMAGE_BINARIES_TABLE_NAME = "ImageBinaries";

        /// <summary>
        /// SQL Server table name storing <see cref="TemplateEntity"/> records.
        /// </summary>
        private const string TEMPLATES_TABLE_NAME = "Templates";

        /// <summary>
        /// SQL Server table name storing <see cref="TemplateImageReference"/> join records.
        /// </summary>
        private const string TEMPLATE_IMAGE_REFERENCES_TABLE_NAME = "TemplateImageReferences";

        /// <summary>
        /// Initializes a new instance of the <see cref="TestContentDbContext"/> class.
        /// </summary>
        /// <param name="options">The configured SQL Server DbContext options.</param>
        public TestContentDbContext(DbContextOptions<TestContentDbContext> options)
                : base(options)
        {
        }

        /// <summary>
        /// Gets the <see cref="ImageEntity"/> metadata table set.
        /// </summary>
        public DbSet<ImageEntity> Images => this.Set<ImageEntity>();

        /// <summary>
        /// Gets the <see cref="ImageBinaryEntity"/> binary payload table set.
        /// </summary>
        public DbSet<ImageBinaryEntity> ImageBinaries => this.Set<ImageBinaryEntity>();

        /// <summary>
        /// Gets the <see cref="TemplateEntity"/> table set.
        /// </summary>
        public DbSet<TemplateEntity> Templates => this.Set<TemplateEntity>();

        /// <summary>
        /// Gets the <see cref="TemplateImageReference"/> join table set.
        /// </summary>
        public DbSet<TemplateImageReference> TemplateImageReferences => this.Set<TemplateImageReference>();

        /// <summary>
        /// Configures relational SQL Server schema mappings for domain aggregates and value projections.
        /// </summary>
        /// <param name="modelBuilder">The EF Core model builder.</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
                ArgumentNullException.ThrowIfNull(modelBuilder);

                // Delegate relational configuration per entity table to keep each mapping self-contained.
                ConfigureImages(modelBuilder: modelBuilder);
                ConfigureImageBinaries(modelBuilder: modelBuilder);
                ConfigureTemplates(modelBuilder: modelBuilder);
                ConfigureTemplateImageReferences(modelBuilder: modelBuilder);
        }

        /// <summary>
        /// Configures table schema, lengths, concurrency token, and indexes for <see cref="ImageEntity"/>.
        /// </summary>
        /// <param name="modelBuilder">The EF Core model builder.</param>
        private static void ConfigureImages(ModelBuilder modelBuilder)
        {
                modelBuilder.Entity<ImageEntity>(entity =>
                {
                        entity.ToTable(IMAGES_TABLE_NAME);
                        entity.HasKey(image => image.Id);
                        // Preserve domain-generated GUID keys assigned inside BaseEntity.InitializeAudit.
                        entity.Property(image => image.Id).ValueGeneratedNever();

                        entity.Property(image => image.StorageNodeId)
                                .HasMaxLength(DomainConstants.MAX_STORAGE_NODE_ID_LENGTH)
                                .IsRequired();
                        entity.Property(image => image.FileName)
                                .HasMaxLength(DomainConstants.MAX_FILE_NAME_LENGTH)
                                .IsRequired();
                        entity.Property(image => image.ContentType)
                                .HasMaxLength(MAX_CONTENT_TYPE_LENGTH)
                                .IsRequired();
                        entity.Property(image => image.FileSizeBytes).IsRequired();
                        // Store lowercase hex SHA-256 digests as fixed-length columns and index for upload deduplication lookups.
                        entity.Property(image => image.Sha256Hash)
                                .HasMaxLength(DomainConstants.SHA256_HEX_LENGTH)
                                .IsFixedLength()
                                .IsRequired();
                        entity.Property(image => image.AltText)
                                .HasMaxLength(DomainConstants.MAX_ALT_TEXT_LENGTH)
                                .IsRequired(false);
                        entity.Property(image => image.IsDeleted).IsRequired();

                        entity.Property(image => image.CreatedAt).IsRequired();
                        entity.Property(image => image.UpdatedAt).IsRequired();
                        entity.Property(image => image.CreatedBy)
                                .HasMaxLength(DomainConstants.MAX_ACTOR_ID_LENGTH)
                                .IsRequired();
                        entity.Property(image => image.UpdatedBy)
                                .HasMaxLength(DomainConstants.MAX_ACTOR_ID_LENGTH)
                                .IsRequired();
                        entity.Property(image => image.ConcurrencyStamp)
                                .IsConcurrencyToken()
                                .IsRequired();

                        // Exclude computed domain projections derived from StorageNodeId and Id.
                        entity.Ignore(image => image.ExternalId);
                        entity.Ignore(image => image.Reference);

                        entity.HasIndex(image => image.Sha256Hash);
                });
        }

        /// <summary>
        /// Configures the 1:1 binary storage table for <see cref="ImageBinaryEntity"/>.
        /// </summary>
        /// <param name="modelBuilder">The EF Core model builder.</param>
        private static void ConfigureImageBinaries(ModelBuilder modelBuilder)
        {
                modelBuilder.Entity<ImageBinaryEntity>(entity =>
                {
                        entity.ToTable(IMAGE_BINARIES_TABLE_NAME);
                        // Share ImageId as both primary key and 1:1 foreign key so VARBINARY payloads remain decoupled from metadata queries.
                        entity.HasKey(binary => binary.ImageId);
                        entity.Property(binary => binary.ImageId).ValueGeneratedNever();
                        entity.Property(binary => binary.Data).IsRequired();

                        entity.HasOne<ImageEntity>()
                                .WithOne()
                                .HasForeignKey<ImageBinaryEntity>(binary => binary.ImageId)
                                .OnDelete(DeleteBehavior.Cascade);
                });
        }

        /// <summary>
        /// Configures table schema, unique name index, backing-field collection, and concurrency token for <see cref="TemplateEntity"/>.
        /// </summary>
        /// <param name="modelBuilder">The EF Core model builder.</param>
        private static void ConfigureTemplates(ModelBuilder modelBuilder)
        {
                modelBuilder.Entity<TemplateEntity>(entity =>
                {
                        entity.ToTable(TEMPLATES_TABLE_NAME);
                        entity.HasKey(template => template.Id);
                        entity.Property(template => template.Id).ValueGeneratedNever();

                        entity.Property(template => template.StorageNodeId)
                                .HasMaxLength(DomainConstants.MAX_STORAGE_NODE_ID_LENGTH)
                                .IsRequired();
                        entity.Property(template => template.Name)
                                .HasMaxLength(DomainConstants.MAX_TEMPLATE_NAME_LENGTH)
                                .IsRequired();
                        entity.Property(template => template.ComponentType).IsRequired();
                        entity.Property(template => template.SchemaJson).IsRequired();
                        entity.Property(template => template.SchemaVersion).IsRequired();
                        entity.Property(template => template.IsArchived).IsRequired();

                        entity.Property(template => template.CreatedAt).IsRequired();
                        entity.Property(template => template.UpdatedAt).IsRequired();
                        entity.Property(template => template.CreatedBy)
                                .HasMaxLength(DomainConstants.MAX_ACTOR_ID_LENGTH)
                                .IsRequired();
                        entity.Property(template => template.UpdatedBy)
                                .HasMaxLength(DomainConstants.MAX_ACTOR_ID_LENGTH)
                                .IsRequired();
                        entity.Property(template => template.ConcurrencyStamp)
                                .IsConcurrencyToken()
                                .IsRequired();

                        // Ignore computed ExternalId and enforce unique template names in SQL Server.
                        entity.Ignore(template => template.ExternalId);
                        entity.HasIndex(template => template.Name).IsUnique();

                        // Materialize ReferencedImages through its private backing field so TemplateEntity preserves read-only encapsulation.
                        entity.HasMany(template => template.ReferencedImages)
                                .WithOne()
                                .HasForeignKey(reference => reference.TemplateId)
                                .OnDelete(DeleteBehavior.Cascade);
                        entity.Navigation(template => template.ReferencedImages)
                                .HasField(REFERENCED_IMAGES_FIELD_NAME)
                                .UsePropertyAccessMode(PropertyAccessMode.Field);
                });
        }

        /// <summary>
        /// Configures the composite key and image index for <see cref="TemplateImageReference"/>.
        /// </summary>
        /// <param name="modelBuilder">The EF Core model builder.</param>
        private static void ConfigureTemplateImageReferences(ModelBuilder modelBuilder)
        {
                modelBuilder.Entity<TemplateImageReference>(entity =>
                {
                        entity.ToTable(TEMPLATE_IMAGE_REFERENCES_TABLE_NAME);
                        // Composite key prevents duplicate image links per template and shard while preserving natural key ordering.
                        entity.HasKey(reference => new
                        {
                                reference.TemplateId,
                                reference.ImageStorageNodeId,
                                reference.ImageId
                        });

                        entity.Property(reference => reference.ImageStorageNodeId)
                                .HasMaxLength(DomainConstants.MAX_STORAGE_NODE_ID_LENGTH)
                                .IsRequired();

                        // Index ImageId directly so DeleteImageUseCase referential-integrity checks avoid full table scans.
                        entity.HasIndex(reference => reference.ImageId);
                });
        }
}

