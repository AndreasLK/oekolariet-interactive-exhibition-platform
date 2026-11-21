using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace ContentService.Tests.Application.Persistence;

/// <summary>
/// Manages an ephemeral Docker SQL Server container and creates/resets the ContentService test database across the test collection.
/// </summary>
/// <remarks>
/// Shares one container across collection classes to amortize SQL Server startup cost while resetting rows between every test.
/// </remarks>
public sealed class MsSqlContentDatabaseFixture : IAsyncLifetime
{
        /// <summary>
        /// Explicit SQL Server 2022 Linux container image used for cross-platform local and CI execution.
        /// </summary>
        private const string MSSQL_DOCKER_IMAGE = "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04";

        /// <summary>
        /// Isolated database catalog name created inside the SQL Server container.
        /// </summary>
        private const string TEST_DATABASE_NAME = "ContentServiceTests";

        /// <summary>
        /// Managed Testcontainers SQL Server instance.
        /// </summary>
        private readonly MsSqlContainer _container;

        /// <summary>
        /// Cached DbContext options configured for the started container instance.
        /// </summary>
        private DbContextOptions<TestContentDbContext>? _dbContextOptions;

        /// <summary>
        /// Cached connection string targeting <see cref="TEST_DATABASE_NAME"/>.
        /// </summary>
        private string _connectionString = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="MsSqlContentDatabaseFixture"/> class.
        /// </summary>
        public MsSqlContentDatabaseFixture()
        {
                // Pass explicit image string to avoid obsolete parameterless builder warnings in Testcontainers 4.x.
                this._container = new MsSqlBuilder(MSSQL_DOCKER_IMAGE).Build();
        }

        /// <summary>
        /// Gets the active SQL Server connection string for raw <see cref="SqlConnection"/> stream readers.
        /// </summary>
        public string ConnectionString => this._connectionString;

        /// <summary>
        /// Starts the SQL Server Docker container and creates the schema via EF Core.
        /// </summary>
        /// <returns>A task representing asynchronous container and schema initialization.</returns>
        public async Task InitializeAsync()
        {
                await this._container.StartAsync();

                // Override InitialCatalog so schema objects live in an isolated test database rather than master.
                SqlConnectionStringBuilder connectionBuilder = new(this._container.GetConnectionString())
                {
                        InitialCatalog = TEST_DATABASE_NAME
                };

                this._connectionString = connectionBuilder.ConnectionString;
                this._dbContextOptions = new DbContextOptionsBuilder<TestContentDbContext>()
                        .UseSqlServer(this._connectionString)
                        .Options;

                // Materialize all tables once per collection run; individual tests reset row data via ResetDatabaseAsync.
                await using TestContentDbContext context = this.CreateDbContext();
                await context.Database.EnsureCreatedAsync();
        }

        /// <summary>
        /// Stops and disposes the SQL Server Docker container after all collection tests finish.
        /// </summary>
        /// <returns>A task representing asynchronous container cleanup.</returns>
        public async Task DisposeAsync()
        {
                // Tear down the shared container only after the entire xUnit collection completes.
                await this._container.DisposeAsync();
        }

        /// <summary>
        /// Creates a fresh <see cref="TestContentDbContext"/> connected to the running SQL Server test database.
        /// </summary>
        /// <returns>A new <see cref="TestContentDbContext"/> instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown when called before <see cref="InitializeAsync"/> completes.</exception>
        public TestContentDbContext CreateDbContext()
        {
                // Fail fast if a test or repository requests a DbContext before InitializeAsync has configured options.
                if (this._dbContextOptions is null)
                {
                        throw new InvalidOperationException("The MSSQL test fixture has not been initialized.");
                }

                return new TestContentDbContext(options: this._dbContextOptions);
        }

        /// <summary>
        /// Clears all table rows in foreign-key dependency order so each test runs against a clean database.
        /// </summary>
        /// <returns>A task representing the asynchronous cleanup operation.</returns>
        public async Task ResetDatabaseAsync()
        {
                await using TestContentDbContext context = this.CreateDbContext();

                // Delete child join/binary tables first to satisfy foreign-key constraints cleanly without dropping the database.
                await context.TemplateImageReferences.ExecuteDeleteAsync();
                await context.Templates.ExecuteDeleteAsync();
                await context.ImageBinaries.ExecuteDeleteAsync();
                await context.Images.ExecuteDeleteAsync();
        }
}

