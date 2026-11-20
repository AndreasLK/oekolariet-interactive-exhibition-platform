
using AuthService.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.Persistence;

public sealed class AuthDbContext
        : IdentityUserContext<AuthIdentityUser, Guid>
{
        public AuthDbContext(DbContextOptions<AuthDbContext> options)
                : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
                base.OnModelCreating(modelBuilder);

                // String conversion avoids coupling persisted roles to enum positions.
                modelBuilder.Entity<AuthIdentityUser>()
                        .Property(user => user.Role)
                        .HasConversion<string>();

                modelBuilder.Entity<AuthPermissionOverride>(
                        buildAction: entity =>
                        {
                                entity.ToTable("AuthPermissionOverrides");

                                // Each account can override a permission only once.
                                entity.HasKey(item => new
                                {
                                        item.UserId,
                                        item.Permission
                                });

                                entity.Property(item => item.Permission)
                                        .HasConversion<string>();

                                // Removing an account also removes its permission overrides.
                                entity.HasOne<AuthIdentityUser>()
                                        .WithMany(user => user.PermissionOverrides)
                                        .HasForeignKey(item => item.UserId)
                                        .OnDelete(DeleteBehavior.Cascade);
                        });
        }
}
