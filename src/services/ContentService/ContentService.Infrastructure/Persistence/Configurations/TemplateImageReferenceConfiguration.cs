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

                // Restrict blocks physical SQL deletion of an Image row whenever any template still links to it (so templates and images never cascade-delete each other).
                builder.HasOne<ImageEntity>()
                        .WithMany()
                        .HasForeignKey(reference => reference.ImageId)
                        .OnDelete(DeleteBehavior.Restrict);
        }
}
