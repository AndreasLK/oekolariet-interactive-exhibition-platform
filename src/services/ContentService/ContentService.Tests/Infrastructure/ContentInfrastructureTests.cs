using System.Security.Cryptography;
using ContentService.Application.Interfaces;
using ContentService.Domain.Common;
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
/// Integration tests verifying <c>ContentService.Infrastructure</c> EF Core migrations, constraints, binary reads, and DI wiring against real SQL Server.
/// </summary>
[Collection(MsSqlContentDatabaseCollection.COLLECTION_NAME)]
[Trait(TestTraits.CATEGORY_KEY, TestTraits.INTEGRATION)]
public sealed class ContentInfrastructureTests : IAsyncLifetime
{
        /// <summary>
        /// Valid storage node identifier used across seeded test entities.
        /// </summary>
        private const string VALID_NODE_ID = "node-1";

        /// <summary>
        /// Valid actor identifier used for audit metadata on seeded records.
        /// </summary>
        private const string VALID_ACTOR_ID = "infra-tester";

        /// <summary>
        /// Multi-chunk payload length (256 KiB) larger than standard 80 KiB copy buffers.
        /// </summary>
        private const int MULTI_CHUNK_PAYLOAD_BYTES = 262_144;

        /// <summary>
        /// Small read probe size used when verifying post-disposal <see cref="ObjectDisposedException"/> guards.
        /// </summary>
        private const int DISPOSED_READ_PROBE_BYTES = 16;

        /// <summary>
        /// Byte modulus used to generate deterministic non-uniform binary payloads.
        /// </summary>
        private const int PAYLOAD_BYTE_MODULUS = 251;

        /// <summary>
        /// Expected SQL Server foreign-key constraint violation error number.
        /// </summary>
        private const int SQL_ERROR_FOREIGN_KEY_CONFLICT = 547;

        /// <summary>
        /// Number of active seeded images counted in the quota monitor assertion.
        /// </summary>
        private const long ACTIVE_SEEDED_IMAGE_COUNT = 2L;

        /// <summary>
        /// Minimal valid PNG signature prefix.
        /// </summary>
        private static readonly byte[] PNG_HEADER_BYTES = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        /// <summary>
        /// Shared SQL Server Testcontainers fixture.
        /// </summary>
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

        /// <summary>
        /// Resets all SQL tables before each test executes.
        /// </summary>
        /// <returns>A task representing asynchronous database cleanup.</returns>
        public Task InitializeAsync() => this._fixture.ResetDatabaseAsync();

        /// <summary>
        /// Completes per-test cleanup without tearing down the shared container.
        /// </summary>
        /// <returns>A completed task.</returns>
        public Task DisposeAsync() => Task.CompletedTask;

        /// <summary>
        /// Verifies that <see cref="ContentDbContext"/> applied <c>InitialCreate</c> cleanly with zero pending model migrations.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task Migrations_WhenAppliedToSqlServer_HaveZeroPendingMigrationsAsync()
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();

                // Act
                IEnumerable<string> applied = await dbContext.Database.GetAppliedMigrationsAsync();
                IEnumerable<string> pending = await dbContext.Database.GetPendingMigrationsAsync();

                // Assert
                Assert.Contains(applied, migration => migration.EndsWith("_InitialCreate", StringComparison.Ordinal));
                Assert.Empty(pending);
        }

        /// <summary>
        /// Verifies that <c>TemplateImageReferences</c> restricts physical SQL deletes on referenced <c>Images</c> while cascading <c>ImageBinaries</c> on unreferenced deletes.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task ForeignKeyDeleteBehavior_ProtectsReferencedImagesAndCascadesUnreferencedBinariesAsync()
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                SqlServerTemplateRepository templateRepository = new(dbContext: dbContext);

                ImageEntity referencedImage = CreateSeededImage(payloadBytes: PNG_HEADER_BYTES);
                ImageEntity unreferencedImage = CreateSeededImage(payloadBytes: PNG_HEADER_BYTES);
                await imageRepository.AddAsync(
                        metadata: referencedImage,
                        binary: ImageBinaryEntity.Create(imageId: referencedImage.Id, data: PNG_HEADER_BYTES),
                        cancellationToken: CancellationToken.None);
                await imageRepository.AddAsync(
                        metadata: unreferencedImage,
                        binary: ImageBinaryEntity.Create(imageId: unreferencedImage.Id, data: PNG_HEADER_BYTES),
                        cancellationToken: CancellationToken.None);

                TemplateEntity template = TemplateEntity.Create(
                        storageNodeId: VALID_NODE_ID,
                        name: "FK Protected Template",
                        componentType: TemplateComponentType.InfoScreen,
                        schemaJson: $"{{\"hero\":\"{referencedImage.Reference}\"}}",
                        actorId: VALID_ACTOR_ID);
                await templateRepository.AddAsync(template: template, cancellationToken: CancellationToken.None);

                // Act & Assert
                SqlException fkException = await Assert.ThrowsAsync<SqlException>(() =>
                        dbContext.Images
                                .Where(image => image.Id == referencedImage.Id)
                                .ExecuteDeleteAsync());
                Assert.Equal(SQL_ERROR_FOREIGN_KEY_CONFLICT, fkException.Number);

                // Deleting an unreferenced image physically must cascade-delete its 1:1 ImageBinaries row.
                await dbContext.Images
                        .Where(image => image.Id == unreferencedImage.Id)
                        .ExecuteDeleteAsync();
                Assert.Null(await this._fixture.GetStoredBinaryAsync(imageId: unreferencedImage.Id, cancellationToken: CancellationToken.None));
        }

        /// <summary>
        /// Verifies that duplicate template names colliding on the SQL unique index translate to <see cref="DomainValidationException"/> in both add and update paths.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task SqlServerTemplateRepository_WhenUniqueNameCollidesInSql_ThrowsDomainValidationExceptionAsync()
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerTemplateRepository templateRepository = new(dbContext: dbContext);
                TemplateEntity first = TemplateEntity.Create(
                        storageNodeId: VALID_NODE_ID,
                        name: "Unique Name",
                        componentType: TemplateComponentType.QuizTemplate,
                        schemaJson: "{}",
                        actorId: VALID_ACTOR_ID);
                TemplateEntity second = TemplateEntity.Create(
                        storageNodeId: VALID_NODE_ID,
                        name: "Second Name",
                        componentType: TemplateComponentType.QuizTemplate,
                        schemaJson: "{}",
                        actorId: VALID_ACTOR_ID);
                TemplateEntity duplicate = TemplateEntity.Create(
                        storageNodeId: VALID_NODE_ID,
                        name: "unique name",
                        componentType: TemplateComponentType.GameTemplate,
                        schemaJson: "{}",
                        actorId: VALID_ACTOR_ID);

                await templateRepository.AddAsync(template: first, cancellationToken: CancellationToken.None);
                await templateRepository.AddAsync(template: second, cancellationToken: CancellationToken.None);

                // Act & Assert
                await Assert.ThrowsAsync<DomainValidationException>(() =>
                        templateRepository.AddAsync(template: duplicate, cancellationToken: CancellationToken.None));

                Guid secondStamp = second.ConcurrencyStamp;
                second.UpdateSchema(name: "UNIQUE NAME", schemaJson: "{}", actorId: VALID_ACTOR_ID);
                await Assert.ThrowsAsync<DomainValidationException>(() =>
                        templateRepository.UpdateAsync(
                                template: second,
                                expectedConcurrencyStamp: secondStamp,
                                cancellationToken: CancellationToken.None));
        }

        /// <summary>
        /// Verifies that <see cref="SqlServerImageBinaryStore.OpenReadAsync"/> loads payloads via EF Core, guards soft-deleted rows, and enforces idempotent disposal.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task SqlServerImageBinaryStore_WhenStreamingMultiChunkPayload_StreamsAccuratelyAndDisposesSafelyAsync()
        {
                // Arrange
                byte[] largePayload = new byte[MULTI_CHUNK_PAYLOAD_BYTES];
                Array.Copy(sourceArray: PNG_HEADER_BYTES, destinationArray: largePayload, length: PNG_HEADER_BYTES.Length);
                for (int index = PNG_HEADER_BYTES.Length; index < largePayload.Length; index++)
                {
                        largePayload[index] = (byte)(index % PAYLOAD_BYTE_MODULUS);
                }

                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                SqlServerImageBinaryStore binaryStore = new(dbContext: dbContext);

                ImageEntity activeImage = CreateSeededImage(payloadBytes: largePayload);
                ImageEntity deletedImage = CreateSeededImage(payloadBytes: PNG_HEADER_BYTES);
                deletedImage.MarkDeleted(actorId: VALID_ACTOR_ID);

                await imageRepository.AddAsync(
                        metadata: activeImage,
                        binary: ImageBinaryEntity.Create(imageId: activeImage.Id, data: largePayload),
                        cancellationToken: CancellationToken.None);
                await imageRepository.AddAsync(
                        metadata: deletedImage,
                        binary: ImageBinaryEntity.Create(imageId: deletedImage.Id, data: PNG_HEADER_BYTES),
                        cancellationToken: CancellationToken.None);

                // Act
                Stream? stream = await binaryStore.OpenReadAsync(imageId: activeImage.Id, cancellationToken: CancellationToken.None);
                Assert.NotNull(stream);
                Assert.True(stream.CanRead);
                Assert.False(stream.CanWrite);

                using MemoryStream copy = new();
                await stream.CopyToAsync(destination: copy, cancellationToken: CancellationToken.None);

                // Dispose twice to verify idempotency and ensure post-disposal reads throw ObjectDisposedException.
                await stream.DisposeAsync();
                await stream.DisposeAsync();

                // Assert
                Assert.Equal(largePayload, copy.ToArray());
                Assert.False(stream.CanRead);
                await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                        await stream.ReadExactlyAsync(buffer: new byte[DISPOSED_READ_PROBE_BYTES], cancellationToken: CancellationToken.None));
                Assert.Throws<ObjectDisposedException>(() =>
                        stream.Read(buffer: new byte[DISPOSED_READ_PROBE_BYTES], offset: 0, count: DISPOSED_READ_PROBE_BYTES));
                Assert.Null(await binaryStore.OpenReadAsync(imageId: deletedImage.Id, cancellationToken: CancellationToken.None));
                Assert.Null(await binaryStore.OpenReadAsync(imageId: Guid.NewGuid(), cancellationToken: CancellationToken.None));

                // Verify synchronous Dispose(), Read(), and Read(Span<byte>) lifecycle path also releases cleanly without throwing.
                Stream? syncStream = await binaryStore.OpenReadAsync(imageId: activeImage.Id, cancellationToken: CancellationToken.None);
                Assert.NotNull(syncStream);
                byte[] syncBuffer = new byte[PNG_HEADER_BYTES.Length];
                syncStream.ReadExactly(buffer: syncBuffer.AsSpan());
                Assert.Equal(PNG_HEADER_BYTES, syncBuffer);
                syncStream.Dispose();
                syncStream.Dispose();
                Assert.False(syncStream.CanRead);
        }

        /// <summary>
        /// Verifies that <see cref="SqlServerImageRepository"/> handles SHA-256 lookups, empty batch queries, missing deletes, and orphaned binary checks cleanly.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task SqlServerImageRepositoryAndBinaryStore_WhenEdgeCasesOccur_BehavePredictablyAsync()
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                SqlServerImageBinaryStore binaryStore = new(dbContext: dbContext);

                ImageEntity activeImage = CreateSeededImage(payloadBytes: PNG_HEADER_BYTES);
                ImageEntity deletedImage = CreateSeededImage(payloadBytes: PNG_HEADER_BYTES);
                deletedImage.MarkDeleted(actorId: VALID_ACTOR_ID);

                await imageRepository.AddAsync(
                        metadata: activeImage,
                        binary: ImageBinaryEntity.Create(imageId: activeImage.Id, data: PNG_HEADER_BYTES),
                        cancellationToken: CancellationToken.None);
                await imageRepository.AddAsync(
                        metadata: deletedImage,
                        binary: ImageBinaryEntity.Create(imageId: deletedImage.Id, data: PNG_HEADER_BYTES),
                        cancellationToken: CancellationToken.None);

                // Seed an orphaned metadata row without ImageBinaries to verify EF Core join returns null cleanly.
                ImageEntity metadataOnlyImage = CreateSeededImage(payloadBytes: PNG_HEADER_BYTES);
                await this._fixture.SeedImageMetadataOnlyAsync(image: metadataOnlyImage, cancellationToken: CancellationToken.None);

                // Act
                ImageEntity? foundByHash = await imageRepository.GetBySha256Async(
                        sha256Hash: activeImage.Sha256Hash,
                        cancellationToken: CancellationToken.None);
                ImageEntity? missingHash = await imageRepository.GetBySha256Async(
                        sha256Hash: new string('0', DomainConstants.SHA256_HEX_LENGTH),
                        cancellationToken: CancellationToken.None);
                IReadOnlyList<ImageEntity> emptyBatch = await imageRepository.GetByIdsAsync(
                        ids: Array.Empty<Guid>(),
                        cancellationToken: CancellationToken.None);
                Stream? orphanedStream = await binaryStore.OpenReadAsync(
                        imageId: metadataOnlyImage.Id,
                        cancellationToken: CancellationToken.None);

                // Assert
                Assert.NotNull(foundByHash);
                Assert.False(foundByHash.IsDeleted);
                Assert.Null(missingHash);
                Assert.Empty(emptyBatch);
                Assert.Null(orphanedStream);

                await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
                        imageRepository.DeleteAsync(
                                image: activeImage,
                                expectedConcurrencyStamp: Guid.NewGuid(),
                                cancellationToken: CancellationToken.None));
        }

        /// <summary>
        /// Verifies that updating a template to remove all image references clears existing <c>TemplateImageReferences</c> join rows.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task SqlServerTemplateRepository_WhenUpdateRemovesAllImageLinks_ClearsJoinRowsAsync()
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                SqlServerTemplateRepository templateRepository = new(dbContext: dbContext);

                ImageEntity image = CreateSeededImage(payloadBytes: PNG_HEADER_BYTES);
                await imageRepository.AddAsync(
                        metadata: image,
                        binary: ImageBinaryEntity.Create(imageId: image.Id, data: PNG_HEADER_BYTES),
                        cancellationToken: CancellationToken.None);

                TemplateEntity template = TemplateEntity.Create(
                        storageNodeId: VALID_NODE_ID,
                        name: "Link Clearing Template",
                        componentType: TemplateComponentType.InfoScreen,
                        schemaJson: $"{{\"hero\":\"{image.Reference}\"}}",
                        actorId: VALID_ACTOR_ID);
                await templateRepository.AddAsync(template: template, cancellationToken: CancellationToken.None);

                Guid currentStamp = template.ConcurrencyStamp;
                template.UpdateSchema(name: "Link Clearing Template", schemaJson: "{}", actorId: VALID_ACTOR_ID);

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
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task SqlServerTemplateRepository_WhenUpdateHasStaleStamp_RollsBackWithoutMutatingLinksAsync()
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                SqlServerTemplateRepository templateRepository = new(dbContext: dbContext);

                ImageEntity initialImage = CreateSeededImage(payloadBytes: PNG_HEADER_BYTES);
                ImageEntity newImage = CreateSeededImage(payloadBytes: PNG_HEADER_BYTES);
                await imageRepository.AddAsync(
                        metadata: initialImage,
                        binary: ImageBinaryEntity.Create(imageId: initialImage.Id, data: PNG_HEADER_BYTES),
                        cancellationToken: CancellationToken.None);
                await imageRepository.AddAsync(
                        metadata: newImage,
                        binary: ImageBinaryEntity.Create(imageId: newImage.Id, data: PNG_HEADER_BYTES),
                        cancellationToken: CancellationToken.None);

                TemplateEntity template = TemplateEntity.Create(
                        storageNodeId: VALID_NODE_ID,
                        name: "Concurrency Guarded",
                        componentType: TemplateComponentType.QuizTemplate,
                        schemaJson: $"{{\"a\":\"{initialImage.Reference}\"}}",
                        actorId: VALID_ACTOR_ID);
                await templateRepository.AddAsync(template: template, cancellationToken: CancellationToken.None);

                // Simulate another writer changing ConcurrencyStamp before this update commits.
                await this._fixture.RotatePersistedConcurrencyStampAsync(
                        templateId: template.Id,
                        newConcurrencyStamp: Guid.NewGuid(),
                        cancellationToken: CancellationToken.None);

                Guid staleStamp = template.ConcurrencyStamp;
                template.UpdateSchema(
                        name: "Stale Rename",
                        schemaJson: $"{{\"b\":\"{newImage.Reference}\"}}",
                        actorId: VALID_ACTOR_ID);

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

                // Verify quota monitor aggregates only active (!IsDeleted) image sizes.
                SqlServerStorageQuotaMonitor quotaMonitor = new(dbContext: dbContext);
                long expectedActiveBytes = PNG_HEADER_BYTES.Length * ACTIVE_SEEDED_IMAGE_COUNT;
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

        /// <summary>
        /// Creates a valid seeded <see cref="ImageEntity"/> whose SHA-256 digest matches <paramref name="payloadBytes"/>.
        /// </summary>
        /// <param name="payloadBytes">The image payload bytes.</param>
        /// <returns>A newly initialized <see cref="ImageEntity"/>.</returns>
        private static ImageEntity CreateSeededImage(byte[] payloadBytes)
        {
                string hash = Convert.ToHexStringLower(SHA256.HashData(payloadBytes));
                return ImageEntity.Create(
                        storageNodeId: VALID_NODE_ID,
                        fileName: "infra-test.png",
                        contentType: DomainConstants.CONTENT_TYPE_PNG,
                        fileSizeBytes: payloadBytes.Length,
                        sha256Hash: hash,
                        actorId: VALID_ACTOR_ID);
        }
}
