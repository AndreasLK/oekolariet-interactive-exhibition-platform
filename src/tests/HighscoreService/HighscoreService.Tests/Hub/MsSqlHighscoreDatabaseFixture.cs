using HighscoreService.Hub.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace HighscoreService.Tests.Hub
{
        /// <summary>
        /// Manages an ephemeral Docker SQL Server container for Hub persistence integration tests.
        /// </summary>
        public sealed class MsSqlHighscoreDatabaseFixture : IAsyncLifetime
        {
                private const string MSSQL_DOCKER_IMAGE = "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04";
                private const string TEST_DATABASE_NAME = "HighscoreServiceTests";

                private readonly MsSqlContainer _container;
                private DbContextOptions<HubHighscoreDbContext>? _dbContextOptions;

                /// <summary>
                /// Initializes a new instance of the <see cref="MsSqlHighscoreDatabaseFixture"/> class.
                /// </summary>
                public MsSqlHighscoreDatabaseFixture()
                {
                        // Pass explicit image string to avoid obsolete parameterless builder warnings in Testcontainers 4.x.
                        this._container = new MsSqlBuilder(MSSQL_DOCKER_IMAGE).Build();
                }

                /// <summary>
                /// Gets the active SQL Server connection string targeting the test database.
                /// </summary>
                public string ConnectionString { get; private set; } = string.Empty;

                /// <summary>
                /// Starts the SQL Server Docker container and provisions the Hub schema.
                /// </summary>
                public async ValueTask InitializeAsync()
                {
                        await this._container.StartAsync();

                        SqlConnectionStringBuilder connectionBuilder = new(this._container.GetConnectionString())
                        {
                                InitialCatalog = TEST_DATABASE_NAME
                        };

                        this.ConnectionString = connectionBuilder.ConnectionString;
                        this._dbContextOptions = new DbContextOptionsBuilder<HubHighscoreDbContext>()
                                .UseSqlServer(this.ConnectionString)
                                .Options;

                        await using HubHighscoreDbContext context = this.CreateDbContext();
                        await context.Database.EnsureCreatedAsync();
                }

                /// <summary>
                /// Stops and disposes the SQL Server Docker container.
                /// </summary>
                public async ValueTask DisposeAsync()
                {
                        await this._container.DisposeAsync();
                }

                /// <summary>
                /// Creates a new <see cref="HubHighscoreDbContext"/> connected to the running SQL Server container.
                /// </summary>
                /// <returns>A fresh <see cref="HubHighscoreDbContext"/> instance.</returns>
                public HubHighscoreDbContext CreateDbContext()
                {
                        if (this._dbContextOptions is null)
                        {
                                throw new InvalidOperationException("Database fixture has not been initialized.");
                        }

                        return new HubHighscoreDbContext(this._dbContextOptions);
                }

                /// <summary>
                /// Clears all highscore and tombstone rows between tests so each test starts with a clean database.
                /// </summary>
                public async Task ResetDatabaseAsync()
                {
                        await using HubHighscoreDbContext context = this.CreateDbContext();
                        await context.Database.ExecuteSqlRawAsync("DELETE FROM [Highscores]; DELETE FROM [GameDeletionTombstones];");
                }
        }
}

