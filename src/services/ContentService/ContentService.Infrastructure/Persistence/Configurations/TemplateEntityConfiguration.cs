using ContentService.Domain.Common;
using ContentService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContentService.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the <see cref="TemplateEntity"/> SQL table, unique name index, optimistic edit-lock token, and private link collection.
/// </summary>
internal sealed class TemplateEntityConfiguration : IEntityTypeConfiguration<TemplateEntity>
{
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<TemplateEntity> builder)
        {
                builder.ToTable(PersistenceConstants.TEMPLATES_TABLE_NAME);
                builder.HasKey(template => template.Id);
                // Disable DB key generation because BaseEntity creates the Guid in C# before SaveChangesAsync(), so references exist immediately without a DB round-trip.
                builder.Property(template => template.Id).ValueGeneratedNever();

                builder.Property(template => template.StorageNodeId)
                        .HasMaxLength(DomainConstants.MAX_STORAGE_NODE_ID_LENGTH)
                        .IsRequired();
                builder.Property(template => template.Name)
                        .HasMaxLength(DomainConstants.MAX_TEMPLATE_NAME_LENGTH)
                        .IsRequired();
                builder.Property(template => template.ComponentType).IsRequired();
                builder.Property(template => template.SchemaJson).IsRequired();
                builder.Property(template => template.SchemaVersion).IsRequired();
                builder.Property(template => template.IsArchived).IsRequired();

                builder.Property(template => template.CreatedAt).IsRequired();
                builder.Property(template => template.UpdatedAt).IsRequired();
                builder.Property(template => template.CreatedBy)
                        .HasMaxLength(DomainConstants.MAX_ACTOR_ID_LENGTH)
                        .IsRequired();
                builder.Property(template => template.UpdatedBy)
                        .HasMaxLength(DomainConstants.MAX_ACTOR_ID_LENGTH)
                        .IsRequired();
                // Optimistic database lock token (random Guid checked in SQL WHERE) preventing two editors from overwriting each other's changes (unrelated to async/await).
                builder.Property(template => template.ConcurrencyStamp)
                        .IsConcurrencyToken()
                        .IsRequired();

                // Exclude derived ExternalId from SQL and enforce unique template names in the database.
                builder.Ignore(template => template.ExternalId);
                builder.HasIndex(template => template.Name).IsUnique();

                // Normal API deletes only set IsArchived = true; on a physical SQL DELETE of a Template row, Cascade removes only the TemplateImageReferences link rows—never the Image rows themselves.
                builder.HasMany(template => template.ReferencedImages)
                        .WithOne()
                        .HasForeignKey(reference => reference.TemplateId)
                        .OnDelete(DeleteBehavior.Cascade);
                // ReferencedImages is exposed as a read-only collection, so EF Core writes directly to the private _referencedImages list when loading from SQL.
                builder.Navigation(template => template.ReferencedImages)
                        .HasField(PersistenceConstants.REFERENCED_IMAGES_FIELD_NAME)
                        .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
}
