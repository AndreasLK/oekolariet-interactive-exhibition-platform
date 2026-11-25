using ContentService.Domain.Entities;
using ContentService.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace ContentService.Tests.Application.Persistence;

/// <summary>
/// Manages an ephemeral Docker SQL Server container, applies EF Core migrations, and resets test data across the collection.
/// </summary>
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
        private DbContextOptions<ContentDbContext>? _dbContextOptions;

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
        /// Gets the active SQL Server connection string targeting the migrated test database.
        /// </summary>
        public string ConnectionString => this._connectionString;

        /// <summary>
        /// Starts the SQL Server Docker container and applies all EF Core migrations.
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
                this._dbContextOptions = new DbContextOptionsBuilder<ContentDbContext>()
                        .UseSqlServer(this._connectionString)
                        .Options;

                // Apply real production EF Core migrations once per collection run.
                await using ContentDbContext context = this.CreateDbContext();
                await context.Database.MigrateAsync();
        }

        /// <summary>
        /// Stops and disposes the SQL Server Docker container after all collection tests finish.
        /// </summary>
        /// <returns>A task representing asynchronous container cleanup.</returns>
        public async Task DisposeAsync()
        {
                await this._container.DisposeAsync();
        }

        /// <summary>
        /// Creates a fresh <see cref="ContentDbContext"/> connected to the running SQL Server test database.
        /// </summary>
        /// <returns>A new <see cref="ContentDbContext"/> instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown when called before <see cref="InitializeAsync"/> completes.</exception>
        public ContentDbContext CreateDbContext()
        {
                if (this._dbContextOptions is null)
                {
                        throw new InvalidOperationException("The MSSQL test fixture has not been initialized.");
                }

                return new ContentDbContext(options: this._dbContextOptions);
        }

        /// <summary>
        /// Clears all table rows in foreign-key dependency order so each test runs against a clean database.
        /// </summary>
        /// <returns>A task representing the asynchronous cleanup operation.</returns>
        public async Task ResetDatabaseAsync()
        {
                await using ContentDbContext context = this.CreateDbContext();

                // Delete child join/binary tables first to satisfy foreign-key constraints cleanly without dropping the database.
                await context.TemplateImageReferences.ExecuteDeleteAsync();
                await context.Templates.ExecuteDeleteAsync();
                await context.ImageBinaries.ExecuteDeleteAsync();
                await context.Images.ExecuteDeleteAsync();
        }

        /// <summary>
        /// Counts all persisted image metadata rows (including soft-deleted records) for test verification.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The total row count in the <c>Images</c> table.</returns>
        public async Task<int> CountStoredImagesAsync(CancellationToken cancellationToken)
        {
                await using ContentDbContext context = this.CreateDbContext();
                return await context.Images.CountAsync(cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Loads a persisted <see cref="ImageBinaryEntity"/> row directly from SQL Server for upload verification.
        /// </summary>
        /// <param name="imageId">The image identifier.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The stored <see cref="ImageBinaryEntity"/>, or <see langword="null"/> if absent.</returns>
        public async Task<ImageBinaryEntity?> GetStoredBinaryAsync(Guid imageId, CancellationToken cancellationToken)
        {
                await using ContentDbContext context = this.CreateDbContext();
                return await context.ImageBinaries
                        .AsNoTracking()
                        .FirstOrDefaultAsync(binary => binary.ImageId == imageId, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Seeds an <see cref="ImageEntity"/> metadata row without a corresponding <see cref="ImageBinaryEntity"/> row to test missing-stream handling.
        /// </summary>
        /// <param name="image">The image metadata entity to insert.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task representing the asynchronous insert operation.</returns>
        public async Task SeedImageMetadataOnlyAsync(ImageEntity image, CancellationToken cancellationToken)
        {
                await using ContentDbContext context = this.CreateDbContext();
                context.Images.Add(image);
                await context.SaveChangesAsync(cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Directly rotates the persisted <see cref="TemplateEntity.ConcurrencyStamp"/> in SQL Server to simulate a concurrent writer.
        /// </summary>
        /// <param name="templateId">The target template identifier.</param>
        /// <param name="newConcurrencyStamp">The replacement concurrency stamp.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task representing the asynchronous SQL update.</returns>
        public async Task RotatePersistedConcurrencyStampAsync(
                Guid templateId,
                Guid newConcurrencyStamp,
                CancellationToken cancellationToken)
        {
                await using ContentDbContext context = this.CreateDbContext();
                await context.Templates
                        .Where(template => template.Id == templateId)
                        .ExecuteUpdateAsync(
                                setters => setters.SetProperty(template => template.ConcurrencyStamp, newConcurrencyStamp),
                                cancellationToken: cancellationToken);
        }
}
