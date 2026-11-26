using System.Security.Cryptography;
using ContentService.Application.Common;
using ContentService.Application.Models;
using ContentService.Application.UseCases.Publishing;
using ContentService.Domain.Common;
using ContentService.Domain.Entities;
using ContentService.Domain.Enums;
using ContentService.Domain.Exceptions;
using ContentService.Domain.ValueObjects;
using ContentService.Infrastructure.Persistence;
using ContentService.Infrastructure.Repositories;
using ContentService.Tests.Application.Persistence;
using ContentService.Tests.Common;

namespace ContentService.Tests.Application;

/// <summary>
/// Integration tests for <see cref="ResolvePublishBundleUseCase"/> executed against a real SQL Server container.
/// </summary>
[Collection(MsSqlContentDatabaseCollection.COLLECTION_NAME)]
[Trait(TestTraits.CATEGORY_KEY, TestTraits.INTEGRATION)]
public sealed class ResolvePublishBundleUseCaseTests : IAsyncLifetime
{
        /// <summary>
        /// Primary storage node identifier used for seeded templates and images.
        /// </summary>
        private const string NODE_ONE_ID = "node-1";

        /// <summary>
        /// Secondary storage node identifier verifying multi-shard asset resolution.
        /// </summary>
        private const string NODE_TWO_ID = "node-2";

        /// <summary>
        /// Valid actor identifier used for audit fields when seeding test records.
        /// </summary>
        private const string VALID_ACTOR_ID = "publisher-1";

        /// <summary>
        /// Expected deduplicated asset count across template schema and implementation JSON.
        /// </summary>
        private const int EXPECTED_ASSET_COUNT = 2;

        /// <summary>
        /// Minimal empty JSON object literal used across validation and negative test cases.
        /// </summary>
        private const string EMPTY_JSON_OBJECT = "{}";

        /// <summary>
        /// Distinct test payload using the 8-byte PNG magic header (<c>0x89 "PNG" CRLF Ctrl+Z LF</c>) + trailing <c>0x01</c> for unique SHA-256 hashing.
        /// </summary>
        private static readonly byte[] FIRST_IMAGE_BYTES = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01];

        /// <summary>
        /// Distinct test payload using the 8-byte PNG magic header + trailing <c>0x02</c> so the second image produces a different SHA-256 hash.
        /// </summary>
        private static readonly byte[] SECOND_IMAGE_BYTES = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x02];

        /// <summary>
        /// Distinct test payload using the 8-byte PNG magic header + trailing <c>0x99</c> to verify publish bundle checksum sensitivity.
        /// </summary>
        private static readonly byte[] MODIFIED_IMAGE_BYTES = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x99];

        /// <summary>
        /// Shared SQL Server test container fixture.
        /// </summary>
        private readonly MsSqlContentDatabaseFixture _fixture;

        /// <summary>
        /// Initializes a new instance of the <see cref="ResolvePublishBundleUseCaseTests"/> class.
        /// </summary>
        /// <param name="fixture">The shared SQL Server container fixture.</param>
        public ResolvePublishBundleUseCaseTests(MsSqlContentDatabaseFixture fixture)
        {
                ArgumentNullException.ThrowIfNull(fixture);
                this._fixture = fixture;
        }

        /// <summary>
        /// Clears all SQL Server tables before each test executes.
        /// </summary>
        /// <returns>A task representing asynchronous database cleanup.</returns>
        public ValueTask InitializeAsync() => new(this._fixture.ResetDatabaseAsync());

        /// <summary>
        /// Completes per-test cleanup without disposing the shared SQL Server container.
        /// </summary>
        /// <returns>A completed task.</returns>
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        /// <summary>
        /// Verifies that picture references are resolved and deduplicated across both template schema and implementation JSON, including archived templates.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task ExecuteAsync_WhenTemplateAndImplementationShareReferences_ResolvesAndDeduplicatesAssetsAsync()
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerTemplateRepository templateRepository = new(dbContext: dbContext);
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                ImageEntity firstImage = CreateSeededImage(storageNodeId: NODE_ONE_ID, payloadBytes: FIRST_IMAGE_BYTES);
                ImageEntity secondImage = CreateSeededImage(storageNodeId: NODE_TWO_ID, payloadBytes: SECOND_IMAGE_BYTES);

                await imageRepository.AddAsync(
                        metadata: firstImage,
                        binary: ImageBinaryEntity.Create(imageId: firstImage.Id, data: FIRST_IMAGE_BYTES),
                        cancellationToken: CancellationToken.None);
                await imageRepository.AddAsync(
                        metadata: secondImage,
                        binary: ImageBinaryEntity.Create(imageId: secondImage.Id, data: SECOND_IMAGE_BYTES),
                        cancellationToken: CancellationToken.None);

                TemplateEntity template = TemplateEntity.Create(
                        storageNodeId: NODE_ONE_ID,
                        name: "Publishable Template",
                        componentType: TemplateComponentType.QuizTemplate,
                        schemaJson: $"{{\"bg\":\"{firstImage.Reference}\"}}",
                        actorId: VALID_ACTOR_ID);
                // Archiving a template must not block resolving active drafts referencing it.
                template.Archive(actorId: VALID_ACTOR_ID);
                await templateRepository.AddAsync(template: template, cancellationToken: CancellationToken.None);

                ResolvePublishBundleUseCase useCase = new(
                        templateRepository: templateRepository,
                        imageRepository: imageRepository);
                string implementationJson = $"{{\"shared\":\"{firstImage.Reference}\",\"icon\":\"{secondImage.Reference}\"}}";

                // Act
                PublishBundleResult bundle = await useCase.ExecuteAsync(
                        command: new ResolvePublishBundleCommand
                        {
                                TemplateId = template.Id,
                                ImplementationJson = implementationJson
                        },
                        cancellationToken: CancellationToken.None);

                // Assert
                // firstImage appears in both Template.SchemaJson and ImplementationJson, so deduplication must yield 2 assets.
                Assert.Equal(template.Id, bundle.TemplateId);
                Assert.Equal(template.SchemaJson, bundle.TemplateSchemaJson);
                Assert.Equal(implementationJson, bundle.ImplementationJson);
                Assert.Equal(EXPECTED_ASSET_COUNT, bundle.Assets.Count);
                Assert.Equal(DomainConstants.SHA256_HEX_LENGTH, bundle.BundleChecksum.Length);
        }

        /// <summary>
        /// Verifies that template-less bundles (<see cref="ResolvePublishBundleCommand.TemplateId"/> is <see langword="null"/>) resolve cleanly.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task ExecuteAsync_WhenTemplateIdIsNull_ResolvesStandaloneImplementationBundleAsync()
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerTemplateRepository templateRepository = new(dbContext: dbContext);
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                ImageEntity image = CreateSeededImage(storageNodeId: NODE_ONE_ID, payloadBytes: FIRST_IMAGE_BYTES);
                await imageRepository.AddAsync(
                        metadata: image,
                        binary: ImageBinaryEntity.Create(imageId: image.Id, data: FIRST_IMAGE_BYTES),
                        cancellationToken: CancellationToken.None);

                ResolvePublishBundleUseCase useCase = new(
                        templateRepository: templateRepository,
                        imageRepository: imageRepository);

                // Act
                PublishBundleResult bundle = await useCase.ExecuteAsync(
                        command: new ResolvePublishBundleCommand
                        {
                                TemplateId = null,
                                ImplementationJson = $"{{\"banner\":\"{image.Reference}\"}}"
                        },
                        cancellationToken: CancellationToken.None);

                // Assert
                // Standalone implementations omit TemplateId and TemplateSchemaJson while still resolving embedded picture references.
                Assert.Null(bundle.TemplateId);
                Assert.Null(bundle.TemplateSchemaJson);
                BundleAssetResult asset = Assert.Single(bundle.Assets);
                Assert.Equal(image.Id, asset.ImageId);
                Assert.Equal(image.Reference.ToString(), asset.PictureReference);
        }

        /// <summary>
        /// Verifies that <see cref="PublishBundleResult.BundleChecksum"/> is invariant to SQL Server row insertion order but changes when asset hashes or JSON change.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task ExecuteAsync_BundleChecksum_IsDeterministicAndSensitiveToContentChangesAsync()
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerTemplateRepository templateRepository = new(dbContext: dbContext);
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                ImageEntity firstImage = CreateSeededImage(storageNodeId: NODE_ONE_ID, payloadBytes: FIRST_IMAGE_BYTES);
                ImageEntity secondImage = CreateSeededImage(storageNodeId: NODE_ONE_ID, payloadBytes: SECOND_IMAGE_BYTES);

                await imageRepository.AddAsync(
                        metadata: firstImage,
                        binary: ImageBinaryEntity.Create(imageId: firstImage.Id, data: FIRST_IMAGE_BYTES),
                        cancellationToken: CancellationToken.None);
                await imageRepository.AddAsync(
                        metadata: secondImage,
                        binary: ImageBinaryEntity.Create(imageId: secondImage.Id, data: SECOND_IMAGE_BYTES),
                        cancellationToken: CancellationToken.None);

                string sharedSchemaJson = $"{{\"a\":\"{firstImage.Reference}\",\"b\":\"{secondImage.Reference}\"}}";
                string implementationJson = $"{{\"x\":\"{secondImage.Reference}\",\"y\":\"{firstImage.Reference}\"}}";
                TemplateEntity templateOne = TemplateEntity.Create(
                        storageNodeId: NODE_ONE_ID,
                        name: "Order One",
                        componentType: TemplateComponentType.Custom,
                        schemaJson: sharedSchemaJson,
                        actorId: VALID_ACTOR_ID);
                await templateRepository.AddAsync(template: templateOne, cancellationToken: CancellationToken.None);

                ResolvePublishBundleUseCase useCase = new(
                        templateRepository: templateRepository,
                        imageRepository: imageRepository);

                // Act
                PublishBundleResult firstBundle = await useCase.ExecuteAsync(
                        command: new ResolvePublishBundleCommand { TemplateId = templateOne.Id, ImplementationJson = implementationJson },
                        cancellationToken: CancellationToken.None);

                // Reset SQL Server and re-seed identical entities in reverse row order to verify deterministic sorting and checksums.
                await this._fixture.ResetDatabaseAsync();
                await imageRepository.AddAsync(
                        metadata: secondImage,
                        binary: ImageBinaryEntity.Create(imageId: secondImage.Id, data: SECOND_IMAGE_BYTES),
                        cancellationToken: CancellationToken.None);
                await imageRepository.AddAsync(
                        metadata: firstImage,
                        binary: ImageBinaryEntity.Create(imageId: firstImage.Id, data: FIRST_IMAGE_BYTES),
                        cancellationToken: CancellationToken.None);
                await templateRepository.AddAsync(template: templateOne, cancellationToken: CancellationToken.None);

                PublishBundleResult secondBundle = await useCase.ExecuteAsync(
                        command: new ResolvePublishBundleCommand { TemplateId = templateOne.Id, ImplementationJson = implementationJson },
                        cancellationToken: CancellationToken.None);
                PublishBundleResult modifiedJsonBundle = await useCase.ExecuteAsync(
                        command: new ResolvePublishBundleCommand
                        {
                                TemplateId = templateOne.Id,
                                ImplementationJson = $"{{\"x\":\"{secondImage.Reference}\",\"y\":\"{firstImage.Reference}\",\"title\":\"Edited\"}}"
                        },
                        cancellationToken: CancellationToken.None);

                ImageEntity changedHashImage = CreateSeededImage(storageNodeId: NODE_ONE_ID, payloadBytes: MODIFIED_IMAGE_BYTES);
                await imageRepository.AddAsync(
                        metadata: changedHashImage,
                        binary: ImageBinaryEntity.Create(imageId: changedHashImage.Id, data: MODIFIED_IMAGE_BYTES),
                        cancellationToken: CancellationToken.None);
                PublishBundleResult changedAssetBundle = await useCase.ExecuteAsync(
                        command: new ResolvePublishBundleCommand
                        {
                                TemplateId = templateOne.Id,
                                ImplementationJson = $"{{\"x\":\"{secondImage.Reference}\",\"y\":\"{changedHashImage.Reference}\"}}"
                        },
                        cancellationToken: CancellationToken.None);

                // Assert
                // Equal checksums and identical asset ordering confirm invariance to SQL row insertion order.
                Assert.Equal(firstBundle.BundleChecksum, secondBundle.BundleChecksum);
                Assert.Equal(firstBundle.Assets.Select(asset => asset.ImageId), secondBundle.Assets.Select(asset => asset.ImageId));
                Assert.NotEqual(firstBundle.BundleChecksum, modifiedJsonBundle.BundleChecksum);
                Assert.NotEqual(firstBundle.BundleChecksum, changedAssetBundle.BundleChecksum);
        }

        /// <summary>
        /// Verifies that missing templates or missing/soft-deleted image references throw <see cref="KeyNotFoundException"/>.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task ExecuteAsync_WhenTemplateOrImageMissing_ThrowsKeyNotFoundExceptionAsync()
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerTemplateRepository templateRepository = new(dbContext: dbContext);
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                ImageEntity deletedImage = CreateSeededImage(storageNodeId: NODE_ONE_ID, payloadBytes: FIRST_IMAGE_BYTES);
                deletedImage.MarkDeleted(actorId: VALID_ACTOR_ID);
                await imageRepository.AddAsync(
                        metadata: deletedImage,
                        binary: ImageBinaryEntity.Create(imageId: deletedImage.Id, data: FIRST_IMAGE_BYTES),
                        cancellationToken: CancellationToken.None);

                ResolvePublishBundleUseCase useCase = new(
                        templateRepository: templateRepository,
                        imageRepository: imageRepository);
                string missingReference = new PictureReference(storageNodeId: NODE_ONE_ID, imageId: Guid.NewGuid()).ToString();

                // Act & Assert
                // Cover missing template ID, non-existent picture reference, and soft-deleted picture reference independently.
                await Assert.ThrowsAsync<KeyNotFoundException>(() => useCase.ExecuteAsync(
                        command: new ResolvePublishBundleCommand { TemplateId = Guid.NewGuid(), ImplementationJson = EMPTY_JSON_OBJECT },
                        cancellationToken: CancellationToken.None));
                await Assert.ThrowsAsync<KeyNotFoundException>(() => useCase.ExecuteAsync(
                        command: new ResolvePublishBundleCommand { TemplateId = null, ImplementationJson = $"{{\"img\":\"{missingReference}\"}}" },
                        cancellationToken: CancellationToken.None));
                await Assert.ThrowsAsync<KeyNotFoundException>(() => useCase.ExecuteAsync(
                        command: new ResolvePublishBundleCommand { TemplateId = null, ImplementationJson = $"{{\"img\":\"{deletedImage.Reference}\"}}" },
                        cancellationToken: CancellationToken.None));
        }

        /// <summary>
        /// Verifies that malformed, primitive, or base64-containing <see cref="ResolvePublishBundleCommand.ImplementationJson"/> throws <see cref="DomainValidationException"/>.
        /// </summary>
        /// <param name="invalidJson">The invalid implementation JSON string.</param>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Theory]
        [InlineData("")]
        [InlineData("{invalid")]
        [InlineData("null")]
        [InlineData("42")]
        [InlineData("{\"img\":\"data:image\\/png;base64,AAAA\"}")]
        public async Task ExecuteAsync_WhenImplementationJsonInvalidOrBase64_ThrowsDomainValidationExceptionAsync(
                string invalidJson)
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerTemplateRepository templateRepository = new(dbContext: dbContext);
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                ResolvePublishBundleUseCase useCase = new(
                        templateRepository: templateRepository,
                        imageRepository: imageRepository);

                // Act & Assert
                // Escaped slash in data:image\/png verifies the validator rejects base64 payloads even after JSON unescaping.
                await Assert.ThrowsAsync<DomainValidationException>(() => useCase.ExecuteAsync(
                        command: new ResolvePublishBundleCommand { TemplateId = null, ImplementationJson = invalidJson },
                        cancellationToken: CancellationToken.None));
        }

        /// <summary>
        /// Verifies that <see cref="Guid.Empty"/> template ID or oversized implementation JSON throws <see cref="DomainValidationException"/>.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task ExecuteAsync_WhenTemplateIdEmptyOrJsonOversized_ThrowsDomainValidationExceptionAsync()
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerTemplateRepository templateRepository = new(dbContext: dbContext);
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                ResolvePublishBundleUseCase useCase = new(
                        templateRepository: templateRepository,
                        imageRepository: imageRepository);
                string oversizedJson = $"{{\"text\":\"{new string('a', ApplicationConstants.MAX_IMPLEMENTATION_JSON_LENGTH)}\"}}";

                // Act & Assert
                // Reject both explicit Guid.Empty template references and payloads exceeding MAX_IMPLEMENTATION_JSON_LENGTH.
                await Assert.ThrowsAsync<DomainValidationException>(() => useCase.ExecuteAsync(
                        command: new ResolvePublishBundleCommand { TemplateId = Guid.Empty, ImplementationJson = EMPTY_JSON_OBJECT },
                        cancellationToken: CancellationToken.None));
                await Assert.ThrowsAsync<DomainValidationException>(() => useCase.ExecuteAsync(
                        command: new ResolvePublishBundleCommand { TemplateId = null, ImplementationJson = oversizedJson },
                        cancellationToken: CancellationToken.None));
        }

        /// <summary>
        /// Creates a seeded <see cref="ImageEntity"/> with a matching SHA-256 digest for the given storage node and payload.
        /// </summary>
        /// <param name="storageNodeId">The target storage node identifier.</param>
        /// <param name="payloadBytes">The raw image payload bytes.</param>
        /// <returns>A newly created <see cref="ImageEntity"/>.</returns>
        private static ImageEntity CreateSeededImage(string storageNodeId, byte[] payloadBytes)
        {
                string hash = Convert.ToHexStringLower(SHA256.HashData(payloadBytes));
                return ImageEntity.Create(
                        storageNodeId: storageNodeId,
                        fileName: "bundle-asset.png",
                        contentType: DomainConstants.CONTENT_TYPE_PNG,
                        fileSizeBytes: payloadBytes.Length,
                        sha256Hash: hash,
                        actorId: VALID_ACTOR_ID);
        }
}
