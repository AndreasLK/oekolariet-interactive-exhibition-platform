using HighscoreService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HighscoreService.Hub.Infrastructure.Persistence.Configurations
{
        /// <summary>
        /// EF Core entity configuration for <see cref="ScreenRegistration"/>.
        /// </summary>
        public sealed class ScreenRegistrationConfiguration : IEntityTypeConfiguration<ScreenRegistration>
        {
                /// <inheritdoc />
                public void Configure(EntityTypeBuilder<ScreenRegistration> builder)
                {
                        // Map client-generated ScreenId primary key and index approval status for queue-cap checks.
                        builder.ToTable("ScreenRegistrations");
                        builder.HasKey(s => s.ScreenId);
                        builder.Property(s => s.ScreenId).ValueGeneratedNever();
                        builder.Property(s => s.NodeId).HasMaxLength(ScreenRegistration.MAX_NODE_ID_LENGTH).IsRequired();
                        builder.Property(s => s.MacAddress).HasMaxLength(17);
                        builder.Property(s => s.SecretHash).HasMaxLength(ScreenRegistration.SECRET_HASH_HEX_LENGTH).IsRequired();
                        builder.HasIndex(s => s.Status);
                }
        }
}

