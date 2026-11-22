using ContentService.Application.Interfaces;
using ContentService.Infrastructure;
using ContentService.Infrastructure.Persistence;
using ContentService.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ContentService.Tests.Infrastructure;

/// <summary>
/// Fast, Docker-free unit tests verifying <c>ContentService.Infrastructure</c> DI registration and constructor guards.
/// </summary>
public sealed class ContentInfrastructureUnitTests
{
        /// <summary>
        /// Sample SQL Server connection string used to verify DI wiring without opening a database connection.
        /// </summary>
        private const string SAMPLE_CONNECTION_STRING = "Server=(localdb)\\mssqllocaldb;Database=ContentServiceUnitTest;Trusted_Connection=True;";

        /// <summary>
        /// Verifies that <see cref="DependencyInjection.AddContentServiceInfrastructure"/> validates null arguments and missing or blank connection strings.
        /// </summary>
        [Fact]
        public void AddContentServiceInfrastructure_WhenArgumentsOrConnectionStringInvalid_ThrowsExpectedException()
        {
                // Arrange
                ServiceCollection services = new();
                IConfiguration emptyConfig = new ConfigurationBuilder().Build();
                IConfiguration blankConfig = new ConfigurationBuilder()
                        .AddInMemoryCollection(new Dictionary<string, string?>
                        {
                                [$"ConnectionStrings:{DependencyInjection.CONTENT_DATABASE_CONNECTION_STRING_NAME}"] = "   "
                        })
                        .Build();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() =>
                        DependencyInjection.AddContentServiceInfrastructure(services: null!, configuration: emptyConfig));
                Assert.Throws<ArgumentNullException>(() =>
                        services.AddContentServiceInfrastructure(configuration: null!));
                Assert.Throws<InvalidOperationException>(() =>
                        services.AddContentServiceInfrastructure(configuration: emptyConfig));
                Assert.Throws<InvalidOperationException>(() =>
                        services.AddContentServiceInfrastructure(configuration: blankConfig));
        }

        /// <summary>
        /// Verifies that <see cref="DependencyInjection.AddContentServiceInfrastructure"/> registers all expected EF Core scoped ports.
        /// </summary>
        [Fact]
        public void AddContentServiceInfrastructure_WhenConfigured_ResolvesScopedEfCoreImplementations()
        {
                // Arrange
                ServiceCollection services = new();
                IConfiguration config = new ConfigurationBuilder()
                        .AddInMemoryCollection(new Dictionary<string, string?>
                        {
                                [$"ConnectionStrings:{DependencyInjection.CONTENT_DATABASE_CONNECTION_STRING_NAME}"] = SAMPLE_CONNECTION_STRING
                        })
                        .Build();

                // Act
                services.AddContentServiceInfrastructure(configuration: config);
                using ServiceProvider provider = services.BuildServiceProvider();
                using IServiceScope scope = provider.CreateScope();

                // Assert
                Assert.NotNull(scope.ServiceProvider.GetRequiredService<ContentDbContext>());
                Assert.IsType<SqlServerImageRepository>(scope.ServiceProvider.GetRequiredService<IImageRepository>());
                Assert.IsType<SqlServerImageBinaryStore>(scope.ServiceProvider.GetRequiredService<IImageBinaryStore>());
                Assert.IsType<SqlServerTemplateRepository>(scope.ServiceProvider.GetRequiredService<ITemplateRepository>());
                Assert.IsType<SqlServerStorageQuotaMonitor>(scope.ServiceProvider.GetRequiredService<IStorageQuotaMonitor>());
        }

        /// <summary>
        /// Verifies that all SQL Server EF Core repositories and <see cref="ContentDbContext"/> enforce null constructor guards.
        /// </summary>
        [Fact]
        public void RepositoriesAndStore_WhenDbContextIsNull_ThrowArgumentNullException()
        {
                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new SqlServerImageRepository(dbContext: null!));
                Assert.Throws<ArgumentNullException>(() => new SqlServerImageBinaryStore(dbContext: null!));
                Assert.Throws<ArgumentNullException>(() => new SqlServerTemplateRepository(dbContext: null!));
                Assert.Throws<ArgumentNullException>(() => new SqlServerStorageQuotaMonitor(dbContext: null!));
        }

        /// <summary>
        /// Verifies that repository methods reject <see langword="null"/> entities and blank names before issuing SQL queries.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task Repositories_WhenPassedNullOrBlankArguments_ThrowArgumentExceptionsAsync()
        {
                // Arrange
                DbContextOptions<ContentDbContext> options = new DbContextOptionsBuilder<ContentDbContext>()
                        .UseSqlServer(SAMPLE_CONNECTION_STRING)
                        .Options;
                await using ContentDbContext dbContext = new(options: options);
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                SqlServerTemplateRepository templateRepository = new(dbContext: dbContext);

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentNullException>(() =>
                        imageRepository.GetByIdsAsync(ids: null!, cancellationToken: CancellationToken.None));
                await Assert.ThrowsAsync<ArgumentNullException>(() =>
                        imageRepository.AddAsync(metadata: null!, binary: null!, cancellationToken: CancellationToken.None));
                await Assert.ThrowsAsync<ArgumentNullException>(() =>
                        imageRepository.DeleteAsync(image: null!, expectedConcurrencyStamp: Guid.NewGuid(), cancellationToken: CancellationToken.None));
                await Assert.ThrowsAsync<ArgumentException>(() =>
                        templateRepository.ExistsByNameAsync(name: "  ", excludeTemplateId: null, cancellationToken: CancellationToken.None));
                await Assert.ThrowsAsync<ArgumentNullException>(() =>
                        templateRepository.AddAsync(template: null!, cancellationToken: CancellationToken.None));
                await Assert.ThrowsAsync<ArgumentNullException>(() =>
                        templateRepository.UpdateAsync(template: null!, expectedConcurrencyStamp: Guid.NewGuid(), cancellationToken: CancellationToken.None));
                await Assert.ThrowsAsync<ArgumentNullException>(() =>
                        templateRepository.DeleteAsync(template: null!, expectedConcurrencyStamp: Guid.NewGuid(), cancellationToken: CancellationToken.None));
        }
}

