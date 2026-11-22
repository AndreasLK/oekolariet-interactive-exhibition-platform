using ContentService.Domain.Common;
using ContentService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContentService.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the <see cref="ImageEntity"/> SQL table, field length bounds, optimistic edit-lock token, and SHA-256 index.
/// </summary>
internal sealed class ImageEntityConfiguration : IEntityTypeConfiguration<ImageEntity>
{
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<ImageEntity> builder)
        {
                builder.ToTable(PersistenceConstants.IMAGES_TABLE_NAME);
                builder.HasKey(image => image.Id);

                // Disable DB key generation because BaseEntity creates the Guid in C# before SaveChangesAsync(), so image references exist immediately without a DB round-trip.
                builder.Property(image => image.Id).ValueGeneratedNever();

                builder.Property(image => image.StorageNodeId)
                        .HasMaxLength(DomainConstants.MAX_STORAGE_NODE_ID_LENGTH)
                        .IsRequired();
                builder.Property(image => image.FileName)
                        .HasMaxLength(DomainConstants.MAX_FILE_NAME_LENGTH)
                        .IsRequired();
                builder.Property(image => image.ContentType)
                        .HasMaxLength(PersistenceConstants.MAX_CONTENT_TYPE_LENGTH)
                        .IsRequired();
                builder.Property(image => image.FileSizeBytes).IsRequired();

                // Fixed-length char(64) storage because a SHA-256 hex digest is always 64 characters long.
                builder.Property(image => image.Sha256Hash)
                        .HasMaxLength(DomainConstants.SHA256_HEX_LENGTH)
                        .IsFixedLength()
                        .IsRequired();
                builder.Property(image => image.AltText)
                        .HasMaxLength(DomainConstants.MAX_ALT_TEXT_LENGTH)
                        .IsRequired(false);
                builder.Property(image => image.AlbumPath)
                        .HasMaxLength(DomainConstants.MAX_ALBUM_PATH_LENGTH)
                        .IsRequired(false);
                builder.Property(image => image.Tags)
                        .HasMaxLength(DomainConstants.MAX_IMAGE_TAGS_LENGTH)
                        .IsRequired(false);
                builder.Property(image => image.IsDeleted).IsRequired();

                builder.Property(image => image.CreatedAt).IsRequired();
                builder.Property(image => image.UpdatedAt).IsRequired();
                builder.Property(image => image.CreatedBy)
                        .HasMaxLength(DomainConstants.MAX_ACTOR_ID_LENGTH)
                        .IsRequired();
                builder.Property(image => image.UpdatedBy)
                        .HasMaxLength(DomainConstants.MAX_ACTOR_ID_LENGTH)
                        .IsRequired();

                // Optimistic database lock token preventing two concurrent delete/edit requests from overwriting each other (unrelated to async/await).
                builder.Property(image => image.ConcurrencyStamp)
                        .IsConcurrencyToken()
                        .IsRequired();

                // Exclude derived properties computed in C# from StorageNodeId/Id/Tags so SQL does not store duplicate columns.
                builder.Ignore(image => image.ExternalId);
                builder.Ignore(image => image.Reference);
                builder.Ignore(image => image.TagList);

                // Keep the Sha256Hash index non-unique because soft-deleted rows (IsDeleted = true) stay in the table even if identical bytes are uploaded again.
                builder.HasIndex(image => image.Sha256Hash);
                builder.HasIndex(image => image.AlbumPath);
        }
}
