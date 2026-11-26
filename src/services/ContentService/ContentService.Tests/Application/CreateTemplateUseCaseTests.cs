using ContentService.Application.Models;
using ContentService.Application.UseCases.Templates;
using ContentService.Domain.Common;
using ContentService.Domain.Entities;
using ContentService.Domain.Enums;
using ContentService.Domain.Exceptions;
using ContentService.Domain.ValueObjects;
using ContentService.Infrastructure.Persistence;
using ContentService.Infrastructure.Repositories;
using ContentService.Tests.Application.Persistence;

namespace ContentService.Tests.Application;

/// <summary>
/// Integration tests for <see cref="CreateTemplateUseCase"/> executed against a real SQL Server container.
/// </summary>
[Collection(MsSqlContentDatabaseCollection.COLLECTION_NAME)]
public sealed class CreateTemplateUseCaseTests : IAsyncLifetime
{
        private const int INVALID_COMPONENT_TYPE_VALUE = 999;

        private readonly MsSqlContentDatabaseFixture _fixture;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateTemplateUseCaseTests"/> class.
        /// </summary>
        /// <param name="fixture">The shared SQL Server container fixture.</param>
        public CreateTemplateUseCaseTests(MsSqlContentDatabaseFixture fixture)
        {
                ArgumentNullException.ThrowIfNull(fixture);
                this._fixture = fixture;
        }

        /// <inheritdoc />
        public Task InitializeAsync() => this._fixture.ResetDatabaseAsync();

        /// <inheritdoc />
        public Task DisposeAsync() => Task.CompletedTask;

        /// <summary>
        /// Verifies that <see cref="CreateTemplateUseCase"/> extracts <c>picture.{node}.{guid}</c> references and persists indexed join rows in SQL Server.
        /// </summary>
        [Fact]
        public async Task CreateTemplateUseCase_WhenValidWithPictureReferences_IndexesReferencesAndPersistsAsync()
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerTemplateRepository templateRepository = new(dbContext: dbContext);
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                ImageEntity image = TemplateUseCaseTestHelpers.CreateSeededImage(storageNodeId: TemplateUseCaseTestHelpers.VALID_NODE_ID);
                await imageRepository.AddAsync(
                        metadata: image,
                        binary: ImageBinaryEntity.Create(imageId: image.Id, data: TemplateUseCaseTestHelpers.SAMPLE_IMAGE_BYTES),
                        cancellationToken: CancellationToken.None);

                CreateTemplateUseCase useCase = TemplateUseCaseTestHelpers.BuildCreateTemplateUseCase(
                        templateRepository: templateRepository,
                        imageRepository: imageRepository);
                string schemaJson = $"{{\"background\":\"{image.Reference}\"}}";

                // Act
                TemplateResult result = await useCase.ExecuteAsync(
                        command: new CreateTemplateCommand
                        {
                                Name = "  Forest Quiz  ",
                                ComponentType = TemplateComponentType.QuizTemplate,
                                SchemaJson = schemaJson,
                                ActorId = TemplateUseCaseTestHelpers.VALID_ACTOR_ID
                        },
                        cancellationToken: CancellationToken.None);

                // Assert
                Assert.Equal("Forest Quiz", result.Name);
                Assert.Equal(TemplateComponentType.QuizTemplate, result.ComponentType);
                Assert.Equal(DomainConstants.INITIAL_VERSION, result.SchemaVersion);
                string singleReference = Assert.Single(result.ReferencedPictureReferences);
                Assert.Equal(image.Reference.ToString(), singleReference);

                TemplateEntity? reloaded = await templateRepository.GetByIdAsync(id: result.Id, cancellationToken: CancellationToken.None);
                Assert.NotNull(reloaded);
                TemplateImageReference persistedLink = Assert.Single(reloaded.ReferencedImages);
                Assert.Equal(image.Id, persistedLink.ImageId);
        }

        /// <summary>
        /// Verifies that invalid JSON, primitive JSON literals, and embedded base64 data URIs throw <see cref="DomainValidationException"/>.
        /// </summary>
        [Theory]
        [InlineData("{not-json")]
        [InlineData("null")]
        [InlineData("123")]
        [InlineData("{\"img\":\"data:image/png;base64,AAAA\"}")]
        public async Task CreateTemplateUseCase_WhenInvalidOrPrimitiveJsonOrBase64_ThrowsDomainValidationExceptionAsync(
                string invalidSchemaJson)
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerTemplateRepository templateRepository = new(dbContext: dbContext);
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                CreateTemplateUseCase useCase = TemplateUseCaseTestHelpers.BuildCreateTemplateUseCase(
                        templateRepository: templateRepository,
                        imageRepository: imageRepository);

                // Act & Assert
                await Assert.ThrowsAsync<DomainValidationException>(() => useCase.ExecuteAsync(
                        command: new CreateTemplateCommand
                        {
                                Name = "Broken Template",
                                ComponentType = TemplateComponentType.InfoScreen,
                                SchemaJson = invalidSchemaJson,
                                ActorId = TemplateUseCaseTestHelpers.VALID_ACTOR_ID
                        },
                        cancellationToken: CancellationToken.None));
        }

        /// <summary>
        /// Verifies that duplicate template names (case-insensitive trimmed) and undefined enum values throw <see cref="DomainValidationException"/>.
        /// </summary>
        [Fact]
        public async Task CreateTemplateUseCase_WhenDuplicateNameOrUndefinedEnum_ThrowsDomainValidationExceptionAsync()
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerTemplateRepository templateRepository = new(dbContext: dbContext);
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                TemplateEntity existing = TemplateEntity.Create(
                        storageNodeId: TemplateUseCaseTestHelpers.VALID_NODE_ID,
                        name: "Water Cycle",
                        componentType: TemplateComponentType.InfoScreen,
                        schemaJson: TemplateUseCaseTestHelpers.EMPTY_JSON_OBJECT,
                        actorId: TemplateUseCaseTestHelpers.VALID_ACTOR_ID);
                await templateRepository.AddAsync(template: existing, cancellationToken: CancellationToken.None);

                CreateTemplateUseCase useCase = TemplateUseCaseTestHelpers.BuildCreateTemplateUseCase(
                        templateRepository: templateRepository,
                        imageRepository: imageRepository);

                // Act & Assert
                await Assert.ThrowsAsync<DomainValidationException>(() => useCase.ExecuteAsync(
                        command: new CreateTemplateCommand
                        {
                                Name = "  water cycle  ",
                                ComponentType = TemplateComponentType.GameTemplate,
                                SchemaJson = TemplateUseCaseTestHelpers.EMPTY_JSON_OBJECT,
                                ActorId = TemplateUseCaseTestHelpers.VALID_ACTOR_ID
                        },
                        cancellationToken: CancellationToken.None));

                await Assert.ThrowsAsync<DomainValidationException>(() => useCase.ExecuteAsync(
                        command: new CreateTemplateCommand
                        {
                                Name = "New Name",
                                ComponentType = (TemplateComponentType)INVALID_COMPONENT_TYPE_VALUE,
                                SchemaJson = TemplateUseCaseTestHelpers.EMPTY_JSON_OBJECT,
                                ActorId = TemplateUseCaseTestHelpers.VALID_ACTOR_ID
                        },
                        cancellationToken: CancellationToken.None));
        }

        /// <summary>
        /// Verifies that referencing a missing, soft-deleted, or wrong-shard picture reference throws <see cref="KeyNotFoundException"/>.
        /// </summary>
        [Fact]
        public async Task CreateTemplateUseCase_WhenImageMissingDeletedOrWrongShard_ThrowsKeyNotFoundExceptionAsync()
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerTemplateRepository templateRepository = new(dbContext: dbContext);
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                ImageEntity deletedImage = TemplateUseCaseTestHelpers.CreateSeededImage(storageNodeId: TemplateUseCaseTestHelpers.VALID_NODE_ID);
                deletedImage.MarkDeleted(actorId: TemplateUseCaseTestHelpers.VALID_ACTOR_ID);
                ImageEntity shardOneImage = TemplateUseCaseTestHelpers.CreateSeededImage(storageNodeId: TemplateUseCaseTestHelpers.VALID_NODE_ID);

                await imageRepository.AddAsync(
                        metadata: deletedImage,
                        binary: ImageBinaryEntity.Create(imageId: deletedImage.Id, data: TemplateUseCaseTestHelpers.SAMPLE_IMAGE_BYTES),
                        cancellationToken: CancellationToken.None);
                await imageRepository.AddAsync(
                        metadata: shardOneImage,
                        binary: ImageBinaryEntity.Create(imageId: shardOneImage.Id, data: TemplateUseCaseTestHelpers.SAMPLE_IMAGE_BYTES),
                        cancellationToken: CancellationToken.None);

                CreateTemplateUseCase useCase = TemplateUseCaseTestHelpers.BuildCreateTemplateUseCase(
                        templateRepository: templateRepository,
                        imageRepository: imageRepository);

                string missingReference = new PictureReference(storageNodeId: TemplateUseCaseTestHelpers.VALID_NODE_ID, imageId: Guid.NewGuid()).ToString();
                string wrongShardReference = new PictureReference(storageNodeId: TemplateUseCaseTestHelpers.OTHER_NODE_ID, imageId: shardOneImage.Id).ToString();

                // Act & Assert
                await Assert.ThrowsAsync<KeyNotFoundException>(() => useCase.ExecuteAsync(
                        command: new CreateTemplateCommand
                        {
                                Name = "Missing Image",
                                ComponentType = TemplateComponentType.Custom,
                                SchemaJson = $"{{\"a\":\"{missingReference}\"}}",
                                ActorId = TemplateUseCaseTestHelpers.VALID_ACTOR_ID
                        },
                        cancellationToken: CancellationToken.None));

                await Assert.ThrowsAsync<KeyNotFoundException>(() => useCase.ExecuteAsync(
                        command: new CreateTemplateCommand
                        {
                                Name = "Deleted Image",
                                ComponentType = TemplateComponentType.Custom,
                                SchemaJson = $"{{\"a\":\"{deletedImage.Reference}\"}}",
                                ActorId = TemplateUseCaseTestHelpers.VALID_ACTOR_ID
                        },
                        cancellationToken: CancellationToken.None));

                await Assert.ThrowsAsync<KeyNotFoundException>(() => useCase.ExecuteAsync(
                        command: new CreateTemplateCommand
                        {
                                Name = "Wrong Shard",
                                ComponentType = TemplateComponentType.Custom,
                                SchemaJson = $"{{\"a\":\"{wrongShardReference}\"}}",
                                ActorId = TemplateUseCaseTestHelpers.VALID_ACTOR_ID
                        },
                        cancellationToken: CancellationToken.None));
        }
}

