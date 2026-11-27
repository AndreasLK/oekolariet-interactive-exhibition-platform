using HighscoreService.Domain.Common;
using HighscoreService.Domain.Entities;
using HighscoreService.Hub.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace HighscoreService.Hub.Infrastructure.Persistence
{
        /// <summary>
        /// SQL Server database context for the central Highscore Hub service.
        /// </summary>
        /// <param name="options">EF Core context options.</param>
        public sealed class HubHighscoreDbContext(DbContextOptions<HubHighscoreDbContext> options) : DbContext(options)
        {
                /// <summary>
                /// Database sequence name used for monotonic Hub sync sequence allocation.
                /// </summary>
                public const string SYNC_SEQUENCE_NAME = "HighscoreSyncSequence";

                /// <summary>
                /// Gets the recorded highscore entries.
                /// </summary>
                public DbSet<HighscoreEntity> Highscores => this.Set<HighscoreEntity>();

                /// <summary>
                /// Gets the deleted-game tombstones.
                /// </summary>
                public DbSet<GameDeletionTombstone> GameDeletionTombstones => this.Set<GameDeletionTombstone>();

                /// <summary>
                /// Gets the enrolled exhibition screen registrations.
                /// </summary>
                public DbSet<ScreenRegistration> ScreenRegistrations => this.Set<ScreenRegistration>();

                /// <inheritdoc />
                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                        base.OnModelCreating(modelBuilder);

                        modelBuilder.HasSequence<long>(SYNC_SEQUENCE_NAME)
                                .StartsAt(1)
                                .IncrementsBy(1);

                        modelBuilder.Entity<HighscoreEntity>(entity =>
                        {
                                entity.ToTable("Highscores");
                                entity.HasKey(e => e.Id);
                                entity.Property(e => e.Id).ValueGeneratedNever();
                                entity.Property(e => e.Name).HasMaxLength(DomainConstants.MAX_PLAYER_NAME_LENGTH);
                                entity.Property(e => e.SenderMacAddress).HasMaxLength(DomainConstants.MAC_ADDRESS_LENGTH).IsRequired();
                                entity.HasIndex(e => new { e.GameId, e.IsDeleted, e.PlayedAtUtc, e.Score });
                                entity.HasIndex(e => e.SyncSequence);
                        });

                        modelBuilder.Entity<GameDeletionTombstone>(entity =>
                        {
                                entity.ToTable("GameDeletionTombstones");
                                entity.HasKey(t => t.GameId);
                                entity.Property(t => t.GameId).ValueGeneratedNever();
                                entity.HasIndex(t => t.SyncSequence);
                        });

                        modelBuilder.ApplyConfiguration(new ScreenRegistrationConfiguration());
                }

                /// <summary>
                /// Allocates the next monotonic <c>SyncSequence</c> from SQL Server (or in-memory fallback for relational/test providers).
                /// </summary>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>The next positive sequence number.</returns>
                public async Task<long> AllocateNextSyncSequenceAsync(CancellationToken cancellationToken)
                {
                        if (this.Database.IsSqlServer())
                        {
                                // Use ToListAsync() instead of SingleAsync() because EF Core wraps SingleAsync() in a
                                // subquery (SELECT TOP(2) FROM (...)), which SQL Server rejects for NEXT VALUE FOR.
                                List<long> values = await this.Database
                                        .SqlQueryRaw<long>($"SELECT NEXT VALUE FOR {SYNC_SEQUENCE_NAME} AS [Value]")
                                        .ToListAsync(cancellationToken);

                                return values[0];
                        }

                        long? maxScoreSeq = await this.Highscores.MaxAsync(e => (long?)e.SyncSequence, cancellationToken);
                        long? maxTombstoneSeq = await this.GameDeletionTombstones.MaxAsync(t => (long?)t.SyncSequence, cancellationToken);
                        return Math.Max(maxScoreSeq ?? 0, maxTombstoneSeq ?? 0) + 1;
                }
        }
}

