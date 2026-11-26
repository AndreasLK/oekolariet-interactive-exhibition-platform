using ContentService.Domain.Common;
using ContentService.Domain.Entities;
using ContentService.Domain.Enums;
using ContentService.Domain.Exceptions;
using ContentService.Infrastructure.Persistence;
using ContentService.Infrastructure.Repositories;
using ContentService.Tests.Application.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ContentService.Tests.Infrastructure;

/// <summary>
/// Integration tests verifying EF Core migrations, foreign-key delete behavior, SQL image repository lookups, and binary streaming against SQL Server.
/// </summary>
[Collection(MsSqlContentDatabaseCollection.COLLECTION_NAME)]
public sealed class SqlServerImageInfrastructureTests : IAsyncLifetime
{
        private const int MULTI_CHUNK_PAYLOAD_BYTES = 262_144;
        private const int DISPOSED_READ_PROBE_BYTES = 16;
        private const int PAYLOAD_BYTE_MODULUS = 251;
        private const int SQL_ERROR_FOREIGN_KEY_CONFLICT = 547;

        private readonly MsSqlContentDatabaseFixture _fixture;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerImageInfrastructureTests"/> class.
        /// </summary>
        /// <param name="fixture">The shared SQL Server container fixture.</param>
        public SqlServerImageInfrastructureTests(MsSqlContentDatabaseFixture fixture)
        {
                ArgumentNullException.ThrowIfNull(fixture);
                this._fixture = fixture;
        }

        /// <inheritdoc />
        public ValueTask InitializeAsync() => new(this._fixture.ResetDatabaseAsync());

        /// <inheritdoc />
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        /// <summary>
        /// Verifies that <see cref="ContentDbContext"/> applied migrations cleanly with zero pending model migrations.
        /// </summary>
        [Fact]
        public async Task Migrations_WhenAppliedToSqlServer_HaveZeroPendingMigrationsAsync()
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();

                // Act
                IEnumerable<string> applied = await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken: TestContext.Current.CancellationToken);
                IEnumerable<string> pending = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken: TestContext.Current.CancellationToken);

                // Assert
                Assert.Contains(applied, migration => migration.EndsWith("_InitialCreate", StringComparison.Ordinal));
                Assert.Empty(pending);
        }

        /// <summary>
        /// Verifies that <c>TemplateImageReferences</c> restricts physical SQL deletes on referenced <c>Images</c> while cascading <c>ImageBinaries</c> on unreferenced deletes.
        /// </summary>
        [Fact]
        public async Task ForeignKeyDeleteBehavior_ProtectsReferencedImagesAndCascadesUnreferencedBinariesAsync()
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                SqlServerTemplateRepository templateRepository = new(dbContext: dbContext);

                ImageEntity referencedImage = InfrastructureTestFixtures.CreateSeededImage(payloadBytes: InfrastructureTestFixtures.PNG_HEADER_BYTES);
                ImageEntity unreferencedImage = InfrastructureTestFixtures.CreateSeededImage(payloadBytes: InfrastructureTestFixtures.PNG_HEADER_BYTES);
                await imageRepository.AddAsync(
                        metadata: referencedImage,
                        binary: ImageBinaryEntity.Create(imageId: referencedImage.Id, data: InfrastructureTestFixtures.PNG_HEADER_BYTES),
                        cancellationToken: CancellationToken.None);
                await imageRepository.AddAsync(
                        metadata: unreferencedImage,
                        binary: ImageBinaryEntity.Create(imageId: unreferencedImage.Id, data: InfrastructureTestFixtures.PNG_HEADER_BYTES),
                        cancellationToken: CancellationToken.None);

                TemplateEntity template = TemplateEntity.Create(
                        storageNodeId: InfrastructureTestFixtures.VALID_NODE_ID,
                        name: "FK Protected Template",
                        componentType: TemplateComponentType.InfoScreen,
                        schemaJson: $"{{\"hero\":\"{referencedImage.Reference}\"}}",
                        actorId: InfrastructureTestFixtures.VALID_ACTOR_ID);
                await templateRepository.AddAsync(template: template, cancellationToken: CancellationToken.None);

                // Act & Assert
                SqlException fkException = await Assert.ThrowsAsync<SqlException>(() =>
                        dbContext.Images
                                .Where(image => image.Id == referencedImage.Id)
                                .ExecuteDeleteAsync(cancellationToken: TestContext.Current.CancellationToken));
                Assert.Equal(SQL_ERROR_FOREIGN_KEY_CONFLICT, fkException.Number);

                await dbContext.Images
                        .Where(image => image.Id == unreferencedImage.Id)
                        .ExecuteDeleteAsync(cancellationToken: TestContext.Current.CancellationToken);
                Assert.Null(await this._fixture.GetStoredBinaryAsync(imageId: unreferencedImage.Id, cancellationToken: CancellationToken.None));
        }

        /// <summary>
        /// Verifies that <see cref="SqlServerImageBinaryStore.OpenReadAsync"/> loads payloads via EF Core, guards soft-deleted rows, and enforces idempotent disposal.
        /// </summary>
        [Fact]
        public async Task SqlServerImageBinaryStore_WhenStreamingMultiChunkPayload_StreamsAccuratelyAndDisposesSafelyAsync()
        {
                // Arrange
                byte[] largePayload = new byte[MULTI_CHUNK_PAYLOAD_BYTES];
                Array.Copy(
                        sourceArray: InfrastructureTestFixtures.PNG_HEADER_BYTES,
                        destinationArray: largePayload,
                        length: InfrastructureTestFixtures.PNG_HEADER_BYTES.Length);
                for (int index = InfrastructureTestFixtures.PNG_HEADER_BYTES.Length; index < largePayload.Length; index++)
                {
                        largePayload[index] = (byte)(index % PAYLOAD_BYTE_MODULUS);
                }

                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                SqlServerImageBinaryStore binaryStore = new(dbContext: dbContext);

                ImageEntity activeImage = InfrastructureTestFixtures.CreateSeededImage(payloadBytes: largePayload);
                ImageEntity deletedImage = InfrastructureTestFixtures.CreateSeededImage(payloadBytes: InfrastructureTestFixtures.PNG_HEADER_BYTES);
                deletedImage.MarkDeleted(actorId: InfrastructureTestFixtures.VALID_ACTOR_ID);

                await imageRepository.AddAsync(
                        metadata: activeImage,
                        binary: ImageBinaryEntity.Create(imageId: activeImage.Id, data: largePayload),
                        cancellationToken: CancellationToken.None);
                await imageRepository.AddAsync(
                        metadata: deletedImage,
                        binary: ImageBinaryEntity.Create(imageId: deletedImage.Id, data: InfrastructureTestFixtures.PNG_HEADER_BYTES),
                        cancellationToken: CancellationToken.None);

                // Act
                Stream? stream = await binaryStore.OpenReadAsync(imageId: activeImage.Id, cancellationToken: CancellationToken.None);
                Assert.NotNull(stream);
                Assert.True(stream.CanRead);
                Assert.False(stream.CanWrite);

                using MemoryStream copy = new();
                await stream.CopyToAsync(destination: copy, cancellationToken: CancellationToken.None);

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

                Stream? syncStream = await binaryStore.OpenReadAsync(imageId: activeImage.Id, cancellationToken: CancellationToken.None);
                Assert.NotNull(syncStream);
                byte[] syncBuffer = new byte[InfrastructureTestFixtures.PNG_HEADER_BYTES.Length];
                syncStream.ReadExactly(buffer: syncBuffer.AsSpan());
                Assert.Equal(InfrastructureTestFixtures.PNG_HEADER_BYTES, syncBuffer);
                syncStream.Dispose();
                syncStream.Dispose();
                Assert.False(syncStream.CanRead);
        }

        /// <summary>
        /// Verifies that <see cref="SqlServerImageRepository"/> handles SHA-256 lookups, empty batch queries, missing deletes, and orphaned binary checks cleanly.
        /// </summary>
        [Fact]
        public async Task SqlServerImageRepositoryAndBinaryStore_WhenEdgeCasesOccur_BehavePredictablyAsync()
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                SqlServerImageBinaryStore binaryStore = new(dbContext: dbContext);

                ImageEntity activeImage = InfrastructureTestFixtures.CreateSeededImage(payloadBytes: InfrastructureTestFixtures.PNG_HEADER_BYTES);
                ImageEntity deletedImage = InfrastructureTestFixtures.CreateSeededImage(payloadBytes: InfrastructureTestFixtures.PNG_HEADER_BYTES);
                deletedImage.MarkDeleted(actorId: InfrastructureTestFixtures.VALID_ACTOR_ID);

                await imageRepository.AddAsync(
                        metadata: activeImage,
                        binary: ImageBinaryEntity.Create(imageId: activeImage.Id, data: InfrastructureTestFixtures.PNG_HEADER_BYTES),
                        cancellationToken: CancellationToken.None);
                await imageRepository.AddAsync(
                        metadata: deletedImage,
                        binary: ImageBinaryEntity.Create(imageId: deletedImage.Id, data: InfrastructureTestFixtures.PNG_HEADER_BYTES),
                        cancellationToken: CancellationToken.None);

                ImageEntity metadataOnlyImage = InfrastructureTestFixtures.CreateSeededImage(payloadBytes: InfrastructureTestFixtures.PNG_HEADER_BYTES);
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
}

