using ContentService.Application.Interfaces;
using ContentService.Domain.Entities;
using ContentService.Domain.Enums;
using ContentService.Domain.Exceptions;
using ContentService.Infrastructure;
using ContentService.Infrastructure.Persistence;
using ContentService.Infrastructure.Repositories;
using ContentService.Tests.Application.Persistence;
using ContentService.Tests.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ContentService.Tests.Infrastructure;

/// <summary>
/// Integration tests verifying <see cref="SqlServerTemplateRepository"/> constraints, join-row updates, quota monitoring, and DI wiring against real SQL Server.
/// </summary>
[Collection(MsSqlContentDatabaseCollection.COLLECTION_NAME)]
[Trait(TestTraits.CATEGORY_KEY, TestTraits.INTEGRATION)]
public sealed class ContentInfrastructureTests : IAsyncLifetime
{
        private const long ACTIVE_SEEDED_IMAGE_COUNT = 2L;

        private readonly MsSqlContentDatabaseFixture _fixture;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContentInfrastructureTests"/> class.
        /// </summary>
        /// <param name="fixture">The shared SQL Server container fixture.</param>
        public ContentInfrastructureTests(MsSqlContentDatabaseFixture fixture)
        {
                ArgumentNullException.ThrowIfNull(fixture);
                this._fixture = fixture;
        }

        /// <inheritdoc />
        public Task InitializeAsync() => this._fixture.ResetDatabaseAsync();

        /// <inheritdoc />
        public Task DisposeAsync() => Task.CompletedTask;

        /// <summary>
        /// Verifies that duplicate template names colliding on the SQL unique index translate to <see cref="DomainValidationException"/> in both add and update paths.
        /// </summary>
        [Fact]
        public async Task SqlServerTemplateRepository_WhenUniqueNameCollidesInSql_ThrowsDomainValidationExceptionAsync()
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerTemplateRepository templateRepository = new(dbContext: dbContext);
                TemplateEntity first = TemplateEntity.Create(
                        storageNodeId: InfrastructureTestHelpers.VALID_NODE_ID,
                        name: "Unique Name",
                        componentType: TemplateComponentType.QuizTemplate,
                        schemaJson: "{}",
                        actorId: InfrastructureTestHelpers.VALID_ACTOR_ID);
                TemplateEntity second = TemplateEntity.Create(
                        storageNodeId: InfrastructureTestHelpers.VALID_NODE_ID,
                        name: "Second Name",
                        componentType: TemplateComponentType.QuizTemplate,
                        schemaJson: "{}",
                        actorId: InfrastructureTestHelpers.VALID_ACTOR_ID);
                TemplateEntity duplicate = TemplateEntity.Create(
                        storageNodeId: InfrastructureTestHelpers.VALID_NODE_ID,
                        name: "unique name",
                        componentType: TemplateComponentType.GameTemplate,
                        schemaJson: "{}",
                        actorId: InfrastructureTestHelpers.VALID_ACTOR_ID);

                await templateRepository.AddAsync(template: first, cancellationToken: CancellationToken.None);
                await templateRepository.AddAsync(template: second, cancellationToken: CancellationToken.None);

                // Act & Assert
                await Assert.ThrowsAsync<DomainValidationException>(() =>
                        templateRepository.AddAsync(template: duplicate, cancellationToken: CancellationToken.None));

                Guid secondStamp = second.ConcurrencyStamp;
                second.UpdateSchema(name: "UNIQUE NAME", schemaJson: "{}", actorId: InfrastructureTestHelpers.VALID_ACTOR_ID);
                await Assert.ThrowsAsync<DomainValidationException>(() =>
                        templateRepository.UpdateAsync(
                                template: second,
                                expectedConcurrencyStamp: secondStamp,
                                cancellationToken: CancellationToken.None));
        }

        /// <summary>
        /// Verifies that updating a template to remove all image references clears existing <c>TemplateImageReferences</c> join rows.
        /// </summary>
        [Fact]
        public async Task SqlServerTemplateRepository_WhenUpdateRemovesAllImageLinks_ClearsJoinRowsAsync()
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                SqlServerTemplateRepository templateRepository = new(dbContext: dbContext);

                ImageEntity image = InfrastructureTestHelpers.CreateSeededImage(payloadBytes: InfrastructureTestHelpers.PNG_HEADER_BYTES);
                await imageRepository.AddAsync(
                        metadata: image,
                        binary: ImageBinaryEntity.Create(imageId: image.Id, data: InfrastructureTestHelpers.PNG_HEADER_BYTES),
                        cancellationToken: CancellationToken.None);

                TemplateEntity template = TemplateEntity.Create(
                        storageNodeId: InfrastructureTestHelpers.VALID_NODE_ID,
                        name: "Link Clearing Template",
                        componentType: TemplateComponentType.InfoScreen,
                        schemaJson: $"{{\"hero\":\"{image.Reference}\"}}",
                        actorId: InfrastructureTestHelpers.VALID_ACTOR_ID);
                await templateRepository.AddAsync(template: template, cancellationToken: CancellationToken.None);

                Guid currentStamp = template.ConcurrencyStamp;
                template.UpdateSchema(name: "Link Clearing Template", schemaJson: "{}", actorId: InfrastructureTestHelpers.VALID_ACTOR_ID);

                // Act
                await templateRepository.UpdateAsync(
                        template: template,
                        expectedConcurrencyStamp: currentStamp,
                        cancellationToken: CancellationToken.None);

                // Assert
                TemplateEntity? reloaded = await templateRepository.GetByIdAsync(id: template.Id, cancellationToken: CancellationToken.None);
                Assert.NotNull(reloaded);
                Assert.Empty(reloaded.ReferencedImages);
        }

        /// <summary>
        /// Verifies that stale concurrency stamps on <see cref="SqlServerTemplateRepository.UpdateAsync"/> roll back without mutating <c>TemplateImageReferences</c>.
        /// </summary>
        [Fact]
        public async Task SqlServerTemplateRepository_WhenUpdateHasStaleStamp_RollsBackWithoutMutatingLinksAsync()
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                SqlServerTemplateRepository templateRepository = new(dbContext: dbContext);

                ImageEntity initialImage = InfrastructureTestHelpers.CreateSeededImage(payloadBytes: InfrastructureTestHelpers.PNG_HEADER_BYTES);
                ImageEntity newImage = InfrastructureTestHelpers.CreateSeededImage(payloadBytes: InfrastructureTestHelpers.PNG_HEADER_BYTES);
                await imageRepository.AddAsync(
                        metadata: initialImage,
                        binary: ImageBinaryEntity.Create(imageId: initialImage.Id, data: InfrastructureTestHelpers.PNG_HEADER_BYTES),
                        cancellationToken: CancellationToken.None);
                await imageRepository.AddAsync(
                        metadata: newImage,
                        binary: ImageBinaryEntity.Create(imageId: newImage.Id, data: InfrastructureTestHelpers.PNG_HEADER_BYTES),
                        cancellationToken: CancellationToken.None);

                TemplateEntity template = TemplateEntity.Create(
                        storageNodeId: InfrastructureTestHelpers.VALID_NODE_ID,
                        name: "Concurrency Guarded",
                        componentType: TemplateComponentType.QuizTemplate,
                        schemaJson: $"{{\"a\":\"{initialImage.Reference}\"}}",
                        actorId: InfrastructureTestHelpers.VALID_ACTOR_ID);
                await templateRepository.AddAsync(template: template, cancellationToken: CancellationToken.None);

                await this._fixture.RotatePersistedConcurrencyStampAsync(
                        templateId: template.Id,
                        newConcurrencyStamp: Guid.NewGuid(),
                        cancellationToken: CancellationToken.None);

                Guid staleStamp = template.ConcurrencyStamp;
                template.UpdateSchema(
                        name: "Stale Rename",
                        schemaJson: $"{{\"b\":\"{newImage.Reference}\"}}",
                        actorId: InfrastructureTestHelpers.VALID_ACTOR_ID);

                // Act & Assert
                await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
                        templateRepository.UpdateAsync(
                                template: template,
                                expectedConcurrencyStamp: staleStamp,
                                cancellationToken: CancellationToken.None));
                await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
                        templateRepository.DeleteAsync(
                                template: template,
                                expectedConcurrencyStamp: staleStamp,
                                cancellationToken: CancellationToken.None));
                await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
                        imageRepository.DeleteAsync(
                                image: initialImage,
                                expectedConcurrencyStamp: Guid.NewGuid(),
                                cancellationToken: CancellationToken.None));

                TemplateEntity? reloaded = await templateRepository.GetByIdAsync(id: template.Id, cancellationToken: CancellationToken.None);
                Assert.NotNull(reloaded);
                Assert.Equal("Concurrency Guarded", reloaded.Name);
                TemplateImageReference link = Assert.Single(reloaded.ReferencedImages);
                Assert.Equal(initialImage.Id, link.ImageId);

                SqlServerStorageQuotaMonitor quotaMonitor = new(dbContext: dbContext);
                long expectedActiveBytes = InfrastructureTestHelpers.PNG_HEADER_BYTES.Length * ACTIVE_SEEDED_IMAGE_COUNT;
                Assert.Equal(expectedActiveBytes, await quotaMonitor.GetUsedBytesAsync(cancellationToken: CancellationToken.None));
        }

        /// <summary>
        /// Verifies that <see cref="DependencyInjection.AddContentServiceInfrastructure"/> validates missing connection strings and resolves all scoped ports.
        /// </summary>
        [Fact]
        public void AddContentServiceInfrastructure_WhenConfiguredOrMissingConnectionString_ValidatesAndResolvesPorts()
        {
                // Arrange
                ServiceCollection emptyServices = new();
                IConfiguration emptyConfig = new ConfigurationBuilder().Build();

                ServiceCollection validServices = new();
                IConfiguration validConfig = new ConfigurationBuilder()
                        .AddInMemoryCollection(new Dictionary<string, string?>
                        {
                                [$"ConnectionStrings:{DependencyInjection.CONTENT_DATABASE_CONNECTION_STRING_NAME}"] = this._fixture.ConnectionString
                        })
                        .Build();

                // Act & Assert
                Assert.Throws<InvalidOperationException>(() => emptyServices.AddContentServiceInfrastructure(configuration: emptyConfig));

                validServices.AddContentServiceInfrastructure(configuration: validConfig);
                using ServiceProvider provider = validServices.BuildServiceProvider();
                using IServiceScope scope = provider.CreateScope();

                Assert.NotNull(scope.ServiceProvider.GetRequiredService<ContentDbContext>());
                Assert.IsType<SqlServerImageRepository>(scope.ServiceProvider.GetRequiredService<IImageRepository>());
                Assert.IsType<SqlServerImageBinaryStore>(scope.ServiceProvider.GetRequiredService<IImageBinaryStore>());
                Assert.IsType<SqlServerTemplateRepository>(scope.ServiceProvider.GetRequiredService<ITemplateRepository>());
                Assert.IsType<SqlServerStorageQuotaMonitor>(scope.ServiceProvider.GetRequiredService<IStorageQuotaMonitor>());
        }
}
