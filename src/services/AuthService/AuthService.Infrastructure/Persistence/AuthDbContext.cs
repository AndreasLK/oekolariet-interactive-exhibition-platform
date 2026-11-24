
using AuthService.Infrastructure.Identity;
using AuthService.Infrastructure.Persistence.Entities;
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

        public DbSet<AuthSessionRecord> AuthSessions =>
                this.Set<AuthSessionRecord>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
                base.OnModelCreating(modelBuilder);

                // Persist role names instead of enum numbers for stability.
                modelBuilder.Entity<AuthIdentityUser>()
                        .Property(user => user.Role)
                        .HasConversion<string>();

                modelBuilder.Entity<AuthPermissionOverride>(
                        buildAction: entity =>
                        {
                                entity.ToTable("AuthPermissionOverrides");

                                // Prevent duplicate overrides for the same permission.
                                entity.HasKey(item => new
                                {
                                        item.UserId,
                                        item.Permission
                                });

                                entity.Property(item => item.Permission)
                                        .HasConversion<string>();

                                entity.HasOne<AuthIdentityUser>()
                                        .WithMany(user => user.PermissionOverrides)
                                        .HasForeignKey(item => item.UserId)
                                        .OnDelete(DeleteBehavior.Cascade);
                        });

                modelBuilder.Entity<AuthSessionRecord>(
                        buildAction: entity =>
                        {
                                entity.ToTable("AuthSessions");

                                entity.HasKey(session => session.Id);

                                // Shared Staff accounts may own multiple sessions.
                                // Restrict deletion to protect session history.
                                entity.HasOne<AuthIdentityUser>()
                                        .WithMany()
                                        .HasForeignKey(session => session.UserId)
                                        .OnDelete(DeleteBehavior.Restrict);

                                entity.HasIndex(session => session.UserId);

                                // Prevent reuse of the same current token hash.
                                entity.HasIndex(
                                        session => session.CurrentRefreshTokenHash)
                                        .IsUnique();

                                // SQLite has no automatic SQL Server-style rowversion.
                                // The repository must rotate Version on every update.
                                entity.Property(session => session.Version)
                                        .IsConcurrencyToken();

                                entity.HasMany(
                                        session => session.ConsumedRefreshTokens)
                                        .WithOne()
                                        .HasForeignKey(
                                                token => token.AuthSessionId)
                                        .OnDelete(DeleteBehavior.Cascade);
                        });

                modelBuilder.Entity<ConsumedRefreshTokenRecord>(
                        buildAction: entity =>
                        {
                                entity.ToTable("AuthConsumedRefreshTokens");

                                // Preserve token history for replay detection.
                                entity.HasKey(token => new
                                {
                                        token.AuthSessionId,
                                        token.TokenHash
                                });

                                entity.Property(token => token.TokenHash)
                                        .HasMaxLength(maxLength: 64)
                                        .IsRequired();

                                // A consumed hash must not belong to multiple sessions.
                                entity.HasIndex(token => token.TokenHash)
                                        .IsUnique();
                        });
        }
}
