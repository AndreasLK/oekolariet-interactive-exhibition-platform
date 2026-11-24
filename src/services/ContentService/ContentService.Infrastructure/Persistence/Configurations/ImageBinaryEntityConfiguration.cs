using ContentService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ContentService.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="ImageBinaryEntity"/> <c>varbinary(max)</c> payloads to a separate 1:1 table so metadata queries never pull megabytes of image bytes from disk.
/// </summary>
internal sealed class ImageBinaryEntityConfiguration : IEntityTypeConfiguration<ImageBinaryEntity>
{
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<ImageBinaryEntity> builder)
        {
                builder.ToTable(PersistenceConstants.IMAGE_BINARIES_TABLE_NAME);

                // Use the C#-generated ImageEntity GUID as both PK and 1:1 FK (ValueGeneratedNever disables SQL IDENTITY/NEWID so IDs exist in C# before SaveChangesAsync).
                builder.HasKey(binary => binary.ImageId);
                builder.Property(binary => binary.ImageId).ValueGeneratedNever();
                builder.Property(binary => binary.Data).IsRequired();

                // Normal API deletes only soft-delete Images (IsDeleted = true); Cascade only cleans up these bytes if an Image row is physically deleted in SQL.
                builder.HasOne<ImageEntity>()
                        .WithOne()
                        .HasForeignKey<ImageBinaryEntity>(binary => binary.ImageId)
                        .OnDelete(DeleteBehavior.Cascade);
        }
}
