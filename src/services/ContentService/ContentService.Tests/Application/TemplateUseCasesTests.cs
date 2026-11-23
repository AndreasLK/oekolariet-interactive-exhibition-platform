using ContentService.Application.Common;
using ContentService.Application.Models;
using ContentService.Application.UseCases.Templates;
using ContentService.Domain.Common;
using ContentService.Domain.Entities;
using ContentService.Domain.Enums;
using ContentService.Domain.Exceptions;
using ContentService.Infrastructure.Persistence;
using ContentService.Infrastructure.Repositories;
using ContentService.Tests.Application.Persistence;

namespace ContentService.Tests.Application;

/// <summary>
/// Integration tests for template update, query, and archive use cases executed against a real SQL Server container.
/// </summary>
[Collection(MsSqlContentDatabaseCollection.COLLECTION_NAME)]
public sealed class TemplateUseCasesTests : IAsyncLifetime
{
        private const int OVERSIZED_PAGE_SIZE = 500;
        private const int EXPECTED_REFERENCED_IMAGE_COUNT = 2;

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

        /// <inheritdoc />
        public ValueTask InitializeAsync() => new(this._fixture.ResetDatabaseAsync());

        /// <inheritdoc />
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        /// <summary>
        /// Verifies that <see cref="UpdateTemplateUseCase"/> reconciles added/removed image links in SQL Server, increments schema version, and detects stale concurrency tokens.
        /// </summary>
        [Fact]
        public async Task UpdateTemplateUseCase_WhenValidOrStaleToken_ReconcilesReferencesOrThrowsConcurrencyConflictAsync()
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerTemplateRepository templateRepository = new(dbContext: dbContext);
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                ImageEntity retainedImage = TemplateUseCaseTestFixtures.CreateSeededImage(storageNodeId: TemplateUseCaseTestFixtures.VALID_NODE_ID);
                ImageEntity removedImage = TemplateUseCaseTestFixtures.CreateSeededImage(storageNodeId: TemplateUseCaseTestFixtures.VALID_NODE_ID);
                ImageEntity addedImage = TemplateUseCaseTestFixtures.CreateSeededImage(storageNodeId: TemplateUseCaseTestFixtures.VALID_NODE_ID);

                await imageRepository.AddAsync(
                        metadata: retainedImage,
                        binary: ImageBinaryEntity.Create(imageId: retainedImage.Id, data: TemplateUseCaseTestFixtures.SAMPLE_IMAGE_BYTES),
                        cancellationToken: CancellationToken.None);
                await imageRepository.AddAsync(
                        metadata: removedImage,
                        binary: ImageBinaryEntity.Create(imageId: removedImage.Id, data: TemplateUseCaseTestFixtures.SAMPLE_IMAGE_BYTES),
                        cancellationToken: CancellationToken.None);
                await imageRepository.AddAsync(
                        metadata: addedImage,
                        binary: ImageBinaryEntity.Create(imageId: addedImage.Id, data: TemplateUseCaseTestFixtures.SAMPLE_IMAGE_BYTES),
                        cancellationToken: CancellationToken.None);

                CreateTemplateUseCase createUseCase = TemplateUseCaseTestFixtures.BuildCreateTemplateUseCase(
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
                                ActorId = TemplateUseCaseTestFixtures.VALID_ACTOR_ID
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
                                ActorId = TemplateUseCaseTestFixtures.VALID_ACTOR_ID
                        },
                        cancellationToken: CancellationToken.None);

                // Assert
                Assert.Equal("Template V2", updated.Name);
                Assert.Equal(DomainConstants.INITIAL_VERSION + 1, updated.SchemaVersion);
                Assert.NotEqual(created.ConcurrencyStamp, updated.ConcurrencyStamp);

                TemplateEntity? reloaded = await templateRepository.GetByIdAsync(id: created.Id, cancellationToken: CancellationToken.None);
                Assert.NotNull(reloaded);
                Assert.Equal(EXPECTED_REFERENCED_IMAGE_COUNT, reloaded.ReferencedImages.Count);
                Assert.Contains(reloaded.ReferencedImages, reference => reference.ImageId == retainedImage.Id);
                Assert.Contains(reloaded.ReferencedImages, reference => reference.ImageId == addedImage.Id);
                Assert.DoesNotContain(reloaded.ReferencedImages, reference => reference.ImageId == removedImage.Id);

                await Assert.ThrowsAsync<ConcurrencyConflictException>(() => updateUseCase.ExecuteAsync(
                        command: new UpdateTemplateCommand
                        {
                                TemplateId = created.Id,
                                Name = "Template V3 Stale",
                                SchemaJson = TemplateUseCaseTestFixtures.EMPTY_JSON_OBJECT,
                                ExpectedConcurrencyStamp = created.ConcurrencyStamp,
                                ActorId = TemplateUseCaseTestFixtures.VALID_ACTOR_ID
                        },
                        cancellationToken: CancellationToken.None));
        }

        /// <summary>
        /// Verifies that <see cref="UpdateTemplateUseCase"/> rejects archived templates and concurrent SQL Server stamp updates cleanly.
        /// </summary>
        [Fact]
        public async Task UpdateTemplateUseCase_WhenArchivedOrRepositoryStampDrifts_ThrowsExpectedExceptionAsync()
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerTemplateRepository templateRepository = new(dbContext: dbContext);
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                TemplateEntity archivedTemplate = TemplateEntity.Create(
                        storageNodeId: TemplateUseCaseTestFixtures.VALID_NODE_ID,
                        name: "Archived",
                        componentType: TemplateComponentType.InfoScreen,
                        schemaJson: TemplateUseCaseTestFixtures.EMPTY_JSON_OBJECT,
                        actorId: TemplateUseCaseTestFixtures.VALID_ACTOR_ID);
                archivedTemplate.Archive(actorId: TemplateUseCaseTestFixtures.VALID_ACTOR_ID);
                await templateRepository.AddAsync(template: archivedTemplate, cancellationToken: CancellationToken.None);

                TemplateEntity activeTemplate = TemplateEntity.Create(
                        storageNodeId: TemplateUseCaseTestFixtures.VALID_NODE_ID,
                        name: "Active",
                        componentType: TemplateComponentType.InfoScreen,
                        schemaJson: TemplateUseCaseTestFixtures.EMPTY_JSON_OBJECT,
                        actorId: TemplateUseCaseTestFixtures.VALID_ACTOR_ID);
                await templateRepository.AddAsync(template: activeTemplate, cancellationToken: CancellationToken.None);
                Guid observedStamp = activeTemplate.ConcurrencyStamp;
                await this._fixture.RotatePersistedConcurrencyStampAsync(
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
                                SchemaJson = TemplateUseCaseTestFixtures.EMPTY_JSON_OBJECT,
                                ExpectedConcurrencyStamp = archivedTemplate.ConcurrencyStamp,
                                ActorId = TemplateUseCaseTestFixtures.VALID_ACTOR_ID
                        },
                        cancellationToken: CancellationToken.None));

                await Assert.ThrowsAsync<ConcurrencyConflictException>(() => updateUseCase.ExecuteAsync(
                        command: new UpdateTemplateCommand
                        {
                                TemplateId = activeTemplate.Id,
                                Name = "Updated Active",
                                SchemaJson = TemplateUseCaseTestFixtures.EMPTY_JSON_OBJECT,
                                ExpectedConcurrencyStamp = observedStamp,
                                ActorId = TemplateUseCaseTestFixtures.VALID_ACTOR_ID
                        },
                        cancellationToken: CancellationToken.None));
        }

        /// <summary>
        /// Verifies that <see cref="UpdateTemplateUseCase"/> rejects renaming a template to a name already used by another template (case-insensitive).
        /// </summary>
        [Fact]
        public async Task UpdateTemplateUseCase_WhenNameConflictsWithExistingTemplate_ThrowsDomainValidationExceptionAsync()
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerTemplateRepository templateRepository = new(dbContext: dbContext);
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                TemplateEntity firstTemplate = TemplateEntity.Create(
                        storageNodeId: TemplateUseCaseTestFixtures.VALID_NODE_ID,
                        name: "First Template",
                        componentType: TemplateComponentType.InfoScreen,
                        schemaJson: TemplateUseCaseTestFixtures.EMPTY_JSON_OBJECT,
                        actorId: TemplateUseCaseTestFixtures.VALID_ACTOR_ID);
                TemplateEntity secondTemplate = TemplateEntity.Create(
                        storageNodeId: TemplateUseCaseTestFixtures.VALID_NODE_ID,
                        name: "Second Template",
                        componentType: TemplateComponentType.InfoScreen,
                        schemaJson: TemplateUseCaseTestFixtures.EMPTY_JSON_OBJECT,
                        actorId: TemplateUseCaseTestFixtures.VALID_ACTOR_ID);
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
                                SchemaJson = TemplateUseCaseTestFixtures.EMPTY_JSON_OBJECT,
                                ExpectedConcurrencyStamp = secondTemplate.ConcurrencyStamp,
                                ActorId = TemplateUseCaseTestFixtures.VALID_ACTOR_ID
                        },
                        cancellationToken: CancellationToken.None));
        }

        /// <summary>
        /// Verifies <see cref="GetTemplateUseCase"/>, <see cref="ListTemplatesUseCase"/>, and <see cref="DeleteTemplateUseCase"/> lifecycle behavior against SQL Server.
        /// </summary>
        [Fact]
        public async Task GetListAndDeleteTemplateUseCases_WhenExecuted_FilterClampAndArchiveProperlyAsync()
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerTemplateRepository templateRepository = new(dbContext: dbContext);
                TemplateEntity quizTemplate = TemplateEntity.Create(
                        storageNodeId: TemplateUseCaseTestFixtures.VALID_NODE_ID,
                        name: "Quiz Alpha",
                        componentType: TemplateComponentType.QuizTemplate,
                        schemaJson: TemplateUseCaseTestFixtures.EMPTY_JSON_OBJECT,
                        actorId: TemplateUseCaseTestFixtures.VALID_ACTOR_ID);
                TemplateEntity gameTemplate = TemplateEntity.Create(
                        storageNodeId: TemplateUseCaseTestFixtures.VALID_NODE_ID,
                        name: "Sorting Beta",
                        componentType: TemplateComponentType.GameTemplate,
                        schemaJson: TemplateUseCaseTestFixtures.EMPTY_JSON_OBJECT,
                        actorId: TemplateUseCaseTestFixtures.VALID_ACTOR_ID);
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
                        command: new DeleteTemplateCommand { TemplateId = quizTemplate.Id, ActorId = TemplateUseCaseTestFixtures.VALID_ACTOR_ID },
                        cancellationToken: CancellationToken.None);
                TemplateResult archivedStillReadable = await getUseCase.ExecuteAsync(
                        templateId: quizTemplate.Id,
                        cancellationToken: CancellationToken.None);

                // Assert
                Assert.Equal(ApplicationConstants.DEFAULT_PAGE, filtered.Page);
                Assert.Equal(ApplicationConstants.MAX_PAGE_SIZE, filtered.PageSize);
                TemplateResult singleMatch = Assert.Single(filtered.Items);
                Assert.Equal(quizTemplate.Id, singleMatch.Id);
                Assert.True(archivedStillReadable.IsArchived);

                await Assert.ThrowsAsync<KeyNotFoundException>(() => deleteUseCase.ExecuteAsync(
                        command: new DeleteTemplateCommand { TemplateId = quizTemplate.Id, ActorId = TemplateUseCaseTestFixtures.VALID_ACTOR_ID },
                        cancellationToken: CancellationToken.None));
                await Assert.ThrowsAsync<KeyNotFoundException>(() => deleteUseCase.ExecuteAsync(
                        command: new DeleteTemplateCommand { TemplateId = Guid.NewGuid(), ActorId = TemplateUseCaseTestFixtures.VALID_ACTOR_ID },
                        cancellationToken: CancellationToken.None));
                await Assert.ThrowsAsync<KeyNotFoundException>(() => getUseCase.ExecuteAsync(
                        templateId: Guid.NewGuid(),
                        cancellationToken: CancellationToken.None));
        }
}
