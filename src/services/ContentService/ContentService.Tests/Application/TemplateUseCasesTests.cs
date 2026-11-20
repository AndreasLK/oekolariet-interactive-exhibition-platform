using System.Security.Cryptography;
using ContentService.Application.Common;
using ContentService.Application.Models;
using ContentService.Application.Options;
using ContentService.Application.UseCases.Templates;
using ContentService.Domain.Common;
using ContentService.Domain.Entities;
using ContentService.Domain.Enums;
using ContentService.Domain.Exceptions;
using ContentService.Domain.ValueObjects;
using ContentService.Tests.Application.Persistence;
using MicrosoftOptions = Microsoft.Extensions.Options.Options;

namespace ContentService.Tests.Application;

/// <summary>
/// Integration tests for template CRUD use cases executed against a real SQL Server container.
/// </summary>
[Collection(MsSqlContentDatabaseCollection.COLLECTION_NAME)]
public sealed class TemplateUseCasesTests : IAsyncLifetime
{
        /// <summary>
        /// Primary storage node identifier configured for template creation and image handle resolution.
        /// </summary>
        private const string VALID_NODE_ID = "node-1";

        /// <summary>
        /// Secondary storage node identifier used to verify wrong-shard handle rejection.
        /// </summary>
        private const string OTHER_NODE_ID = "node-2";

        /// <summary>
        /// Valid actor identifier recorded on created, updated, and archived templates.
        /// </summary>
        private const string VALID_ACTOR_ID = "editor-1";

        /// <summary>
        /// Undefined <see cref="TemplateComponentType"/> numeric value used to verify enum validation.
        /// </summary>
        private const int INVALID_COMPONENT_TYPE_VALUE = 999;

        /// <summary>
        /// Oversized page size used to verify upper-bound clamping in <see cref="ListTemplatesUseCase"/>.
        /// </summary>
        private const int OVERSIZED_PAGE_SIZE = 500;

        /// <summary>
        /// Expected number of <see cref="TemplateImageReference"/> join rows after reconciling template updates.
        /// </summary>
        private const int EXPECTED_REFERENCED_IMAGE_COUNT = 2;

        /// <summary>
        /// Minimal empty JSON object literal used for template schema fixtures.
        /// </summary>
        private const string EMPTY_JSON_OBJECT = "{}";

        /// <summary>
        /// Minimal valid PNG header bytes used when seeding referenced images.
        /// </summary>
        private static readonly byte[] SAMPLE_IMAGE_BYTES = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        /// <summary>
        /// Shared SQL Server test container fixture.
        /// </summary>
        private readonly MsSqlContentDatabaseFixture _fixture;

        /// <summary>
        /// Initializes a new instance of the <see cref="TemplateUseCasesTests"/> class.
        /// </summary>
        /// <param name="fixture">The shared SQL Server container fixture.</param>
        public TemplateUseCasesTests(MsSqlContentDatabaseFixture fixture)
        {
                ArgumentNullException.ThrowIfNull(fixture);
                this._fixture = fixture;
        }

        /// <summary>
        /// Clears all SQL Server tables before each test executes.
        /// </summary>
        /// <returns>A task representing asynchronous database cleanup.</returns>
        public Task InitializeAsync() => this._fixture.ResetDatabaseAsync();

        /// <summary>
        /// Completes per-test cleanup without disposing the shared SQL Server container.
        /// </summary>
        /// <returns>A completed task.</returns>
        public Task DisposeAsync() => Task.CompletedTask;

        /// <summary>
        /// Verifies that <see cref="CreateTemplateUseCase"/> extracts <c>picture.{node}.{guid}</c> references and persists indexed join rows in SQL Server.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task CreateTemplateUseCase_WhenValidWithPictureHandles_IndexesReferencesAndPersistsAsync()
        {
                // Arrange
                SqlServerTemplateRepository templateRepository = new(fixture: this._fixture);
                SqlServerImageRepository imageRepository = new(fixture: this._fixture);
                ImageEntity image = CreateSeededImage(storageNodeId: VALID_NODE_ID);
                await imageRepository.AddAsync(
                        metadata: image,
                        binary: ImageBinaryEntity.Create(imageId: image.Id, data: SAMPLE_IMAGE_BYTES),
                        cancellationToken: CancellationToken.None);

                CreateTemplateUseCase useCase = BuildCreateTemplateUseCase(
                        templateRepository: templateRepository,
                        imageRepository: imageRepository);
                string schemaJson = $"{{\"background\":\"{image.Reference}\"}}";

                // Act
                // Include surrounding whitespace in Name to verify trimming before persistence.
                TemplateResult result = await useCase.ExecuteAsync(
                        command: new CreateTemplateCommand
                        {
                                Name = "  Forest Quiz  ",
                                ComponentType = TemplateComponentType.QuizTemplate,
                                SchemaJson = schemaJson,
                                ActorId = VALID_ACTOR_ID
                        },
                        cancellationToken: CancellationToken.None);

                // Assert
                Assert.Equal("Forest Quiz", result.Name);
                Assert.Equal(TemplateComponentType.QuizTemplate, result.ComponentType);
                Assert.Equal(DomainConstants.INITIAL_VERSION, result.SchemaVersion);
                string singleHandle = Assert.Single(result.ReferencedPictureHandles);
                Assert.Equal(image.Reference.ToString(), singleHandle);

                TemplateEntity? reloaded = await templateRepository.GetByIdAsync(id: result.Id, cancellationToken: CancellationToken.None);
                Assert.NotNull(reloaded);
                TemplateImageReference persistedLink = Assert.Single(reloaded.ReferencedImages);
                Assert.Equal(image.Id, persistedLink.ImageId);
        }

        /// <summary>
        /// Verifies that invalid JSON, primitive JSON literals, and embedded base64 data URIs throw <see cref="DomainValidationException"/>.
        /// </summary>
        /// <param name="invalidSchemaJson">The malformed or disallowed JSON schema string.</param>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Theory]
        [InlineData("{not-json")]
        [InlineData("null")]
        [InlineData("123")]
        [InlineData("{\"img\":\"data:image/png;base64,AAAA\"}")]
        public async Task CreateTemplateUseCase_WhenInvalidOrPrimitiveJsonOrBase64_ThrowsDomainValidationExceptionAsync(
                string invalidSchemaJson)
        {
                // Arrange
                SqlServerTemplateRepository templateRepository = new(fixture: this._fixture);
                SqlServerImageRepository imageRepository = new(fixture: this._fixture);
                CreateTemplateUseCase useCase = BuildCreateTemplateUseCase(
                        templateRepository: templateRepository,
                        imageRepository: imageRepository);

                // Act & Assert
                await Assert.ThrowsAsync<DomainValidationException>(() => useCase.ExecuteAsync(
                        command: new CreateTemplateCommand
                        {
                                Name = "Broken Template",
                                ComponentType = TemplateComponentType.InfoScreen,
                                SchemaJson = invalidSchemaJson,
                                ActorId = VALID_ACTOR_ID
                        },
                        cancellationToken: CancellationToken.None));
        }

        /// <summary>
        /// Verifies that duplicate template names (case-insensitive trimmed) and undefined enum values throw <see cref="DomainValidationException"/>.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task CreateTemplateUseCase_WhenDuplicateNameOrUndefinedEnum_ThrowsDomainValidationExceptionAsync()
        {
                // Arrange
                SqlServerTemplateRepository templateRepository = new(fixture: this._fixture);
                SqlServerImageRepository imageRepository = new(fixture: this._fixture);
                TemplateEntity existing = TemplateEntity.Create(
                        storageNodeId: VALID_NODE_ID,
                        name: "Water Cycle",
                        componentType: TemplateComponentType.InfoScreen,
                        schemaJson: EMPTY_JSON_OBJECT,
                        actorId: VALID_ACTOR_ID);
                await templateRepository.AddAsync(template: existing, cancellationToken: CancellationToken.None);

                CreateTemplateUseCase useCase = BuildCreateTemplateUseCase(
                        templateRepository: templateRepository,
                        imageRepository: imageRepository);

                // Act & Assert
                // Duplicate detection must normalize casing and surrounding whitespace before comparing names.
                await Assert.ThrowsAsync<DomainValidationException>(() => useCase.ExecuteAsync(
                        command: new CreateTemplateCommand
                        {
                                Name = "  water cycle  ",
                                ComponentType = TemplateComponentType.GameTemplate,
                                SchemaJson = EMPTY_JSON_OBJECT,
                                ActorId = VALID_ACTOR_ID
                        },
                        cancellationToken: CancellationToken.None));

                await Assert.ThrowsAsync<DomainValidationException>(() => useCase.ExecuteAsync(
                        command: new CreateTemplateCommand
                        {
                                Name = "New Name",
                                ComponentType = (TemplateComponentType)INVALID_COMPONENT_TYPE_VALUE,
                                SchemaJson = EMPTY_JSON_OBJECT,
                                ActorId = VALID_ACTOR_ID
                        },
                        cancellationToken: CancellationToken.None));
        }

        /// <summary>
        /// Verifies that referencing a missing, soft-deleted, or wrong-shard image handle throws <see cref="KeyNotFoundException"/>.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task CreateTemplateUseCase_WhenImageMissingDeletedOrWrongShard_ThrowsKeyNotFoundExceptionAsync()
        {
                // Arrange
                SqlServerTemplateRepository templateRepository = new(fixture: this._fixture);
                SqlServerImageRepository imageRepository = new(fixture: this._fixture);
                ImageEntity deletedImage = CreateSeededImage(storageNodeId: VALID_NODE_ID);
                deletedImage.MarkDeleted(actorId: VALID_ACTOR_ID);
                ImageEntity shardOneImage = CreateSeededImage(storageNodeId: VALID_NODE_ID);

                await imageRepository.AddAsync(
                        metadata: deletedImage,
                        binary: ImageBinaryEntity.Create(imageId: deletedImage.Id, data: SAMPLE_IMAGE_BYTES),
                        cancellationToken: CancellationToken.None);
                await imageRepository.AddAsync(
                        metadata: shardOneImage,
                        binary: ImageBinaryEntity.Create(imageId: shardOneImage.Id, data: SAMPLE_IMAGE_BYTES),
                        cancellationToken: CancellationToken.None);

                CreateTemplateUseCase useCase = BuildCreateTemplateUseCase(
                        templateRepository: templateRepository,
                        imageRepository: imageRepository);

                // Build handles pointing to a missing GUID and to an existing image ID under a mismatched storage node shard.
                string missingHandle = new PictureReference(storageNodeId: VALID_NODE_ID, imageId: Guid.NewGuid()).ToString();
                string wrongShardHandle = new PictureReference(storageNodeId: OTHER_NODE_ID, imageId: shardOneImage.Id).ToString();

                // Act & Assert
                await Assert.ThrowsAsync<KeyNotFoundException>(() => useCase.ExecuteAsync(
                        command: new CreateTemplateCommand
                        {
                                Name = "Missing Image",
                                ComponentType = TemplateComponentType.Custom,
                                SchemaJson = $"{{\"a\":\"{missingHandle}\"}}",
                                ActorId = VALID_ACTOR_ID
                        },
                        cancellationToken: CancellationToken.None));

                await Assert.ThrowsAsync<KeyNotFoundException>(() => useCase.ExecuteAsync(
                        command: new CreateTemplateCommand
                        {
                                Name = "Deleted Image",
                                ComponentType = TemplateComponentType.Custom,
                                SchemaJson = $"{{\"a\":\"{deletedImage.Reference}\"}}",
                                ActorId = VALID_ACTOR_ID
                        },
                        cancellationToken: CancellationToken.None));

                await Assert.ThrowsAsync<KeyNotFoundException>(() => useCase.ExecuteAsync(
                        command: new CreateTemplateCommand
                        {
                                Name = "Wrong Shard",
                                ComponentType = TemplateComponentType.Custom,
                                SchemaJson = $"{{\"a\":\"{wrongShardHandle}\"}}",
                                ActorId = VALID_ACTOR_ID
                        },
                        cancellationToken: CancellationToken.None));
        }

        /// <summary>
        /// Verifies that <see cref="UpdateTemplateUseCase"/> reconciles added/removed image links in SQL Server, increments schema version, and detects stale concurrency tokens.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task UpdateTemplateUseCase_WhenValidOrStaleToken_ReconcilesReferencesOrThrowsConcurrencyConflictAsync()
        {
                // Arrange
                SqlServerTemplateRepository templateRepository = new(fixture: this._fixture);
                SqlServerImageRepository imageRepository = new(fixture: this._fixture);
                ImageEntity retainedImage = CreateSeededImage(storageNodeId: VALID_NODE_ID);
                ImageEntity removedImage = CreateSeededImage(storageNodeId: VALID_NODE_ID);
                ImageEntity addedImage = CreateSeededImage(storageNodeId: VALID_NODE_ID);

                await imageRepository.AddAsync(
                        metadata: retainedImage,
                        binary: ImageBinaryEntity.Create(imageId: retainedImage.Id, data: SAMPLE_IMAGE_BYTES),
                        cancellationToken: CancellationToken.None);
                await imageRepository.AddAsync(
                        metadata: removedImage,
                        binary: ImageBinaryEntity.Create(imageId: removedImage.Id, data: SAMPLE_IMAGE_BYTES),
                        cancellationToken: CancellationToken.None);
                await imageRepository.AddAsync(
                        metadata: addedImage,
                        binary: ImageBinaryEntity.Create(imageId: addedImage.Id, data: SAMPLE_IMAGE_BYTES),
                        cancellationToken: CancellationToken.None);

                CreateTemplateUseCase createUseCase = BuildCreateTemplateUseCase(
                        templateRepository: templateRepository,
                        imageRepository: imageRepository);
                UpdateTemplateUseCase updateUseCase = new(
                        templateRepository: templateRepository,
                        imageRepository: imageRepository);

                TemplateResult created = await createUseCase.ExecuteAsync(
                        command: new CreateTemplateCommand
                        {
                                Name = "Template V1",
                                ComponentType = TemplateComponentType.QuizTemplate,
                                SchemaJson = $"{{\"keep\":\"{retainedImage.Reference}\",\"drop\":\"{removedImage.Reference}\"}}",
                                ActorId = VALID_ACTOR_ID
                        },
                        cancellationToken: CancellationToken.None);

                // Act
                TemplateResult updated = await updateUseCase.ExecuteAsync(
                        command: new UpdateTemplateCommand
                        {
                                TemplateId = created.Id,
                                Name = "Template V2",
                                SchemaJson = $"{{\"keep\":\"{retainedImage.Reference}\",\"add\":\"{addedImage.Reference}\"}}",
                                ExpectedConcurrencyStamp = created.ConcurrencyStamp,
                                ActorId = VALID_ACTOR_ID
                        },
                        cancellationToken: CancellationToken.None);

                // Assert
                Assert.Equal("Template V2", updated.Name);
                Assert.Equal(DomainConstants.INITIAL_VERSION + 1, updated.SchemaVersion);
                Assert.NotEqual(created.ConcurrencyStamp, updated.ConcurrencyStamp);

                // Verify SQL join table reconciled retained+added links and removed dropped links.
                TemplateEntity? reloaded = await templateRepository.GetByIdAsync(id: created.Id, cancellationToken: CancellationToken.None);
                Assert.NotNull(reloaded);
                Assert.Equal(EXPECTED_REFERENCED_IMAGE_COUNT, reloaded.ReferencedImages.Count);
                Assert.Contains(reloaded.ReferencedImages, reference => reference.ImageId == retainedImage.Id);
                Assert.Contains(reloaded.ReferencedImages, reference => reference.ImageId == addedImage.Id);
                Assert.DoesNotContain(reloaded.ReferencedImages, reference => reference.ImageId == removedImage.Id);

                // Reusing the original concurrency stamp after the first update must raise ConcurrencyConflictException.
                await Assert.ThrowsAsync<ConcurrencyConflictException>(() => updateUseCase.ExecuteAsync(
                        command: new UpdateTemplateCommand
                        {
                                TemplateId = created.Id,
                                Name = "Template V3 Stale",
                                SchemaJson = EMPTY_JSON_OBJECT,
                                ExpectedConcurrencyStamp = created.ConcurrencyStamp,
                                ActorId = VALID_ACTOR_ID
                        },
                        cancellationToken: CancellationToken.None));
        }

        /// <summary>
        /// Verifies that <see cref="UpdateTemplateUseCase"/> rejects archived templates and concurrent SQL Server stamp updates cleanly.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task UpdateTemplateUseCase_WhenArchivedOrRepositoryStampDrifts_ThrowsExpectedExceptionAsync()
        {
                // Arrange
                SqlServerTemplateRepository templateRepository = new(fixture: this._fixture);
                SqlServerImageRepository imageRepository = new(fixture: this._fixture);
                TemplateEntity archivedTemplate = TemplateEntity.Create(
                        storageNodeId: VALID_NODE_ID,
                        name: "Archived",
                        componentType: TemplateComponentType.InfoScreen,
                        schemaJson: EMPTY_JSON_OBJECT,
                        actorId: VALID_ACTOR_ID);
                archivedTemplate.Archive(actorId: VALID_ACTOR_ID);
                await templateRepository.AddAsync(template: archivedTemplate, cancellationToken: CancellationToken.None);

                TemplateEntity activeTemplate = TemplateEntity.Create(
                        storageNodeId: VALID_NODE_ID,
                        name: "Active",
                        componentType: TemplateComponentType.InfoScreen,
                        schemaJson: EMPTY_JSON_OBJECT,
                        actorId: VALID_ACTOR_ID);
                await templateRepository.AddAsync(template: activeTemplate, cancellationToken: CancellationToken.None);
                Guid observedStamp = activeTemplate.ConcurrencyStamp;
                // Simulate a concurrent writer rotating the persisted row stamp after our read.
                await templateRepository.RotatePersistedConcurrencyStampAsync(
                        templateId: activeTemplate.Id,
                        newConcurrencyStamp: Guid.NewGuid(),
                        cancellationToken: CancellationToken.None);

                UpdateTemplateUseCase updateUseCase = new(
                        templateRepository: templateRepository,
                        imageRepository: imageRepository);

                // Act & Assert
                await Assert.ThrowsAsync<DomainValidationException>(() => updateUseCase.ExecuteAsync(
                        command: new UpdateTemplateCommand
                        {
                                TemplateId = archivedTemplate.Id,
                                Name = "Updated Archived",
                                SchemaJson = EMPTY_JSON_OBJECT,
                                ExpectedConcurrencyStamp = archivedTemplate.ConcurrencyStamp,
                                ActorId = VALID_ACTOR_ID
                        },
                        cancellationToken: CancellationToken.None));

                await Assert.ThrowsAsync<ConcurrencyConflictException>(() => updateUseCase.ExecuteAsync(
                        command: new UpdateTemplateCommand
                        {
                                TemplateId = activeTemplate.Id,
                                Name = "Updated Active",
                                SchemaJson = EMPTY_JSON_OBJECT,
                                ExpectedConcurrencyStamp = observedStamp,
                                ActorId = VALID_ACTOR_ID
                        },
                        cancellationToken: CancellationToken.None));
        }

        /// <summary>
        /// Verifies that <see cref="UpdateTemplateUseCase"/> rejects renaming a template to a name already used by another template (case-insensitive).
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task UpdateTemplateUseCase_WhenNameConflictsWithExistingTemplate_ThrowsDomainValidationExceptionAsync()
        {
                // Arrange
                SqlServerTemplateRepository templateRepository = new(fixture: this._fixture);
                SqlServerImageRepository imageRepository = new(fixture: this._fixture);
                TemplateEntity firstTemplate = TemplateEntity.Create(
                        storageNodeId: VALID_NODE_ID,
                        name: "First Template",
                        componentType: TemplateComponentType.InfoScreen,
                        schemaJson: EMPTY_JSON_OBJECT,
                        actorId: VALID_ACTOR_ID);
                TemplateEntity secondTemplate = TemplateEntity.Create(
                        storageNodeId: VALID_NODE_ID,
                        name: "Second Template",
                        componentType: TemplateComponentType.InfoScreen,
                        schemaJson: EMPTY_JSON_OBJECT,
                        actorId: VALID_ACTOR_ID);
                await templateRepository.AddAsync(template: firstTemplate, cancellationToken: CancellationToken.None);
                await templateRepository.AddAsync(template: secondTemplate, cancellationToken: CancellationToken.None);

                UpdateTemplateUseCase updateUseCase = new(
                        templateRepository: templateRepository,
                        imageRepository: imageRepository);

                // Act & Assert
                await Assert.ThrowsAsync<DomainValidationException>(() => updateUseCase.ExecuteAsync(
                        command: new UpdateTemplateCommand
                        {
                                TemplateId = secondTemplate.Id,
                                Name = "first template",
                                SchemaJson = EMPTY_JSON_OBJECT,
                                ExpectedConcurrencyStamp = secondTemplate.ConcurrencyStamp,
                                ActorId = VALID_ACTOR_ID
                        },
                        cancellationToken: CancellationToken.None));
        }

        /// <summary>
        /// Verifies <see cref="GetTemplateUseCase"/>, <see cref="ListTemplatesUseCase"/>, and <see cref="DeleteTemplateUseCase"/> lifecycle behavior against SQL Server.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task GetListAndDeleteTemplateUseCases_WhenExecuted_FilterClampAndArchiveProperlyAsync()
        {
                // Arrange
                SqlServerTemplateRepository templateRepository = new(fixture: this._fixture);
                TemplateEntity quizTemplate = TemplateEntity.Create(
                        storageNodeId: VALID_NODE_ID,
                        name: "Quiz Alpha",
                        componentType: TemplateComponentType.QuizTemplate,
                        schemaJson: EMPTY_JSON_OBJECT,
                        actorId: VALID_ACTOR_ID);
                TemplateEntity gameTemplate = TemplateEntity.Create(
                        storageNodeId: VALID_NODE_ID,
                        name: "Sorting Beta",
                        componentType: TemplateComponentType.GameTemplate,
                        schemaJson: EMPTY_JSON_OBJECT,
                        actorId: VALID_ACTOR_ID);
                await templateRepository.AddAsync(template: quizTemplate, cancellationToken: CancellationToken.None);
                await templateRepository.AddAsync(template: gameTemplate, cancellationToken: CancellationToken.None);

                GetTemplateUseCase getUseCase = new(templateRepository: templateRepository);
                ListTemplatesUseCase listUseCase = new(templateRepository: templateRepository);
                DeleteTemplateUseCase deleteUseCase = new(templateRepository: templateRepository);

                // Act
                PagedResult<TemplateResult> filtered = await listUseCase.ExecuteAsync(
                        query: new ListTemplatesQuery
                        {
                                Page = 0,
                                PageSize = OVERSIZED_PAGE_SIZE,
                                ComponentType = TemplateComponentType.QuizTemplate,
                                SearchTerm = "alpha"
                        },
                        cancellationToken: CancellationToken.None);

                await deleteUseCase.ExecuteAsync(
                        command: new DeleteTemplateCommand { TemplateId = quizTemplate.Id, ActorId = VALID_ACTOR_ID },
                        cancellationToken: CancellationToken.None);
                // Archiving is a soft-delete: GetTemplateUseCase must still return the archived template record.
                TemplateResult archivedStillReadable = await getUseCase.ExecuteAsync(
                        templateId: quizTemplate.Id,
                        cancellationToken: CancellationToken.None);

                // Assert
                Assert.Equal(ApplicationConstants.DEFAULT_PAGE, filtered.Page);
                Assert.Equal(ApplicationConstants.MAX_PAGE_SIZE, filtered.PageSize);
                TemplateResult singleMatch = Assert.Single(filtered.Items);
                Assert.Equal(quizTemplate.Id, singleMatch.Id);
                Assert.True(archivedStillReadable.IsArchived);

                // Re-deleting an already archived template or unknown template ID must throw KeyNotFoundException.
                await Assert.ThrowsAsync<KeyNotFoundException>(() => deleteUseCase.ExecuteAsync(
                        command: new DeleteTemplateCommand { TemplateId = quizTemplate.Id, ActorId = VALID_ACTOR_ID },
                        cancellationToken: CancellationToken.None));
                await Assert.ThrowsAsync<KeyNotFoundException>(() => deleteUseCase.ExecuteAsync(
                        command: new DeleteTemplateCommand { TemplateId = Guid.NewGuid(), ActorId = VALID_ACTOR_ID },
                        cancellationToken: CancellationToken.None));
                await Assert.ThrowsAsync<KeyNotFoundException>(() => getUseCase.ExecuteAsync(
                        templateId: Guid.NewGuid(),
                        cancellationToken: CancellationToken.None));
        }

        /// <summary>
        /// Constructs a <see cref="CreateTemplateUseCase"/> wired to the real SQL Server repositories.
        /// </summary>
        /// <param name="templateRepository">The SQL Server template repository.</param>
        /// <param name="imageRepository">The SQL Server image repository.</param>
        /// <returns>A configured <see cref="CreateTemplateUseCase"/> instance.</returns>
        private static CreateTemplateUseCase BuildCreateTemplateUseCase(
                SqlServerTemplateRepository templateRepository,
                SqlServerImageRepository imageRepository)
        {
                ContentStorageOptions options = new() { StorageNodeId = VALID_NODE_ID };
                return new CreateTemplateUseCase(
                        templateRepository: templateRepository,
                        imageRepository: imageRepository,
                        options: MicrosoftOptions.Create(options));
        }

        /// <summary>
        /// Creates a valid <see cref="ImageEntity"/> metadata record for the requested storage node.
        /// </summary>
        /// <param name="storageNodeId">The target storage node identifier.</param>
        /// <returns>A newly created <see cref="ImageEntity"/>.</returns>
        private static ImageEntity CreateSeededImage(string storageNodeId)
        {
                string hash = Convert.ToHexStringLower(SHA256.HashData(SAMPLE_IMAGE_BYTES));
                return ImageEntity.Create(
                        storageNodeId: storageNodeId,
                        fileName: "asset.png",
                        contentType: DomainConstants.CONTENT_TYPE_PNG,
                        fileSizeBytes: SAMPLE_IMAGE_BYTES.Length,
                        sha256Hash: hash,
                        actorId: VALID_ACTOR_ID);
        }
}
