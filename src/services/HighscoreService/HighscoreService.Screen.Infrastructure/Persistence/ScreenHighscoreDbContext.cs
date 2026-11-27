using System.Data.Common;
using HighscoreService.Domain.Common;
using HighscoreService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HighscoreService.Screen.Infrastructure.Persistence
{
        /// <summary>
        /// SQLite database context for slim Screen servers with UTC-tick conversions and incremental auto-vacuum.
        /// </summary>
        /// <param name="options">EF Core context options.</param>
        public sealed class ScreenHighscoreDbContext(DbContextOptions<ScreenHighscoreDbContext> options) : DbContext(options)
        {

                /// <summary>
                /// Gets the local cached/recorded highscore entries.
                /// </summary>
                public DbSet<HighscoreEntity> Highscores => this.Set<HighscoreEntity>();

                /// <summary>
                /// Gets the deleted-game tombstones.
                /// </summary>
                public DbSet<GameDeletionTombstone> GameDeletionTombstones => this.Set<GameDeletionTombstone>();

                /// <summary>
                /// Gets the pending outbox entries waiting to be pushed to the Hub.
                /// </summary>
                public DbSet<ScreenOutboxEntry> OutboxEntries => this.Set<ScreenOutboxEntry>();

                /// <summary>
                /// Gets the per-(source, game) pull watermarks.
                /// </summary>
                public DbSet<ScreenSyncWatermark> SyncWatermarks => this.Set<ScreenSyncWatermark>();

                /// <summary>
                /// Ensures <c>PRAGMA auto_vacuum=INCREMENTAL</c> is active in the SQLite file header.
                /// </summary>
                /// <param name="cancellationToken">Cancellation token.</param>
                public async Task EnsureIncrementalAutoVacuumAsync(CancellationToken cancellationToken)
                {
                        await this.Database.OpenConnectionAsync(cancellationToken);
                        try
                        {
                                DbConnection connection = this.Database.GetDbConnection();
                                await using DbCommand checkCmd = connection.CreateCommand();
                                checkCmd.CommandText = "PRAGMA auto_vacuum;";
                                object? raw = await checkCmd.ExecuteScalarAsync(cancellationToken);
                                long mode = raw is null ? 0 : Convert.ToInt64(raw);

                                // Mode 2 = INCREMENTAL; requires a one-time VACUUM outside any transaction if not yet enabled.
                                if (mode != 2)
                                {
                                        await using DbCommand setCmd = connection.CreateCommand();
                                        setCmd.CommandText = "PRAGMA auto_vacuum=INCREMENTAL; VACUUM;";
                                        await setCmd.ExecuteNonQueryAsync(cancellationToken);
                                }
                        }
                        finally
                        {
                                await this.Database.CloseConnectionAsync();
                        }
                }

                /// <inheritdoc />
                protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
                {
                        base.ConfigureConventions(configurationBuilder);

                        // EF Core SQLite cannot ORDER BY DateTimeOffset natively; store as UTC ticks (Int64).
                        configurationBuilder.Properties<DateTimeOffset>()
                                .HaveConversion<UtcTicksConverter>();
                        configurationBuilder.Properties<DateTimeOffset?>()
                                .HaveConversion<NullableUtcTicksConverter>();
                }

                /// <inheritdoc />
                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                        base.OnModelCreating(modelBuilder);

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
                        });

                        modelBuilder.Entity<ScreenOutboxEntry>(entity =>
                        {
                                entity.ToTable("HighscoreOutbox");
                                entity.HasKey(o => o.HighscoreId);
                                entity.Property(o => o.HighscoreId).ValueGeneratedNever();
                        });

                        modelBuilder.Entity<ScreenSyncWatermark>(entity =>
                        {
                                entity.ToTable("HighscoreSyncWatermarks");
                                entity.HasKey(w => new { w.SourceNodeId, w.GameId });
                                entity.Property(w => w.SourceNodeId).HasMaxLength(128);
                        });
                }
        }
}

