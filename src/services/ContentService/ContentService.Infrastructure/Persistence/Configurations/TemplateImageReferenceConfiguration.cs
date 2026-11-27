using ContentService.Domain.Common;
using ContentService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContentService.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configures the template-to-image join table so reference checks stay fast and images cannot be deleted while used by any template.
/// </summary>
internal sealed class TemplateImageReferenceConfiguration : IEntityTypeConfiguration<TemplateImageReference>
{
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<TemplateImageReference> builder)
        {
                builder.ToTable(PersistenceConstants.TEMPLATE_IMAGE_REFERENCES_TABLE_NAME);

                // Composite primary key prevents duplicate link rows for the same template, storage node label, and image.
                builder.HasKey(reference => new
                {
                        reference.TemplateId,
                        reference.ImageStorageNodeId,
                        reference.ImageId
                });

                builder.Property(reference => reference.ImageStorageNodeId)
                        .HasMaxLength(DomainConstants.MAX_STORAGE_NODE_ID_LENGTH)
                        .IsRequired();

                // Index ImageId separately because TemplateId is first in the composite key; without this, DeleteImageUseCase would scan the whole table.
                builder.HasIndex(reference => reference.ImageId);

                // WHY: Do NOT configure a local SQL Server foreign key to Images here.
                // In multi-node deployments, a template stored on node-1 can reference an image stored on node-2's separate database;
                // a local SQL FK would crash cross-node template saves with SQL Error 547.
                // Cross-node referential integrity is enforced at the application layer via ReferencedImageVerifier and DeleteImageUseCase.
        }
}
