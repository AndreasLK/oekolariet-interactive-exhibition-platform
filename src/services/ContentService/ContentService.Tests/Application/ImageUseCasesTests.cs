using ContentService.Application.Common;
using ContentService.Application.Models;
using ContentService.Application.UseCases.Images;
using ContentService.Domain.Entities;
using ContentService.Domain.Enums;
using ContentService.Domain.Exceptions;
using ContentService.Infrastructure.Persistence;
using ContentService.Infrastructure.Repositories;
using ContentService.Tests.Application.Persistence;
using ContentService.Tests.Common;
using MicrosoftOptions = Microsoft.Extensions.Options.Options;

namespace ContentService.Tests.Application;

/// <summary>
/// Integration tests for image read, list, and delete use cases executed against a real SQL Server container.
/// </summary>
[Collection(MsSqlContentDatabaseCollection.COLLECTION_NAME)]
[Trait(TestTraits.CATEGORY_KEY, TestTraits.INTEGRATION)]
public sealed class ImageUseCasesTests : IAsyncLifetime
{
        private const int NEGATIVE_PAGE_NUMBER = -5;
        private const int OVERSIZED_PAGE_SIZE = 500;

        private readonly MsSqlContentDatabaseFixture _fixture;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageUseCasesTests"/> class.
        /// </summary>
        /// <param name="fixture">The shared SQL Server container fixture.</param>
        public ImageUseCasesTests(MsSqlContentDatabaseFixture fixture)
        {
                ArgumentNullException.ThrowIfNull(fixture);
                this._fixture = fixture;
        }

        /// <inheritdoc />
        public ValueTask InitializeAsync() => new(this._fixture.ResetDatabaseAsync());

        /// <inheritdoc />
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        /// <summary>
        /// Verifies that <see cref="GetImageMetadataUseCase"/> returns active metadata and throws <see cref="KeyNotFoundException"/> for soft-deleted or unknown images.
        /// </summary>
        [Fact]
        public async Task GetImageMetadataUseCase_WhenActiveOrDeletedOrMissing_BehavesAsExpectedAsync()
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                ImageEntity activeImage = ImageUseCaseTestFixtures.CreateSeededImage(payloadBytes: ImageUseCaseTestFixtures.VALID_PNG_PAYLOAD);
                ImageEntity deletedImage = ImageUseCaseTestFixtures.CreateSeededImage(payloadBytes: ImageUseCaseTestFixtures.VALID_PNG_PAYLOAD);
                deletedImage.MarkDeleted(actorId: ImageUseCaseTestFixtures.VALID_ACTOR_ID);

                await imageRepository.AddAsync(
                        metadata: activeImage,
                        binary: ImageBinaryEntity.Create(imageId: activeImage.Id, data: ImageUseCaseTestFixtures.VALID_PNG_PAYLOAD),
                        cancellationToken: CancellationToken.None);
                await imageRepository.AddAsync(
                        metadata: deletedImage,
                        binary: ImageBinaryEntity.Create(imageId: deletedImage.Id, data: ImageUseCaseTestFixtures.VALID_PNG_PAYLOAD),
                        cancellationToken: CancellationToken.None);

                GetImageMetadataUseCase useCase = new(imageRepository: imageRepository);

                // Act
                ImageMetadataResult found = await useCase.ExecuteAsync(imageId: activeImage.Id, cancellationToken: CancellationToken.None);

                // Assert
                Assert.Equal(activeImage.Id, found.Id);
                await Assert.ThrowsAsync<DomainValidationException>(() =>
                        useCase.ExecuteAsync(imageId: Guid.Empty, cancellationToken: CancellationToken.None));
                await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                        useCase.ExecuteAsync(imageId: deletedImage.Id, cancellationToken: CancellationToken.None));
                await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                        useCase.ExecuteAsync(imageId: Guid.NewGuid(), cancellationToken: CancellationToken.None));
        }

        /// <summary>
        /// Verifies that <see cref="GetImageStreamUseCase"/> streams real image bytes from SQL Server and throws <see cref="KeyNotFoundException"/> when the binary row is missing.
        /// </summary>
        [Fact]
        public async Task GetImageStreamUseCase_WhenActiveOrMissingBinary_StreamsBytesOrThrowsKeyNotFoundAsync()
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                CountingImageBinaryStore binaryStore = new(innerStore: new SqlServerImageBinaryStore(dbContext: dbContext));
                ImageEntity image = ImageUseCaseTestFixtures.CreateSeededImage(payloadBytes: ImageUseCaseTestFixtures.VALID_PNG_PAYLOAD);
                ImageBinaryEntity binary = ImageBinaryEntity.Create(imageId: image.Id, data: ImageUseCaseTestFixtures.VALID_PNG_PAYLOAD);
                await imageRepository.AddAsync(metadata: image, binary: binary, cancellationToken: CancellationToken.None);

                // Seed an orphaned metadata row without ImageBinaries to exercise missing-stream KeyNotFoundException handling.
                ImageEntity metadataOnlyImage = ImageUseCaseTestFixtures.CreateSeededImage(payloadBytes: ImageUseCaseTestFixtures.VALID_PNG_PAYLOAD);
                await this._fixture.SeedImageMetadataOnlyAsync(image: metadataOnlyImage, cancellationToken: CancellationToken.None);

                GetImageStreamUseCase useCase = new(
                        imageRepository: imageRepository,
                        imageBinaryStore: binaryStore);

                // Act
                await using ImageStreamResult result = await useCase.ExecuteAsync(
                        query: new GetImageStreamQuery { ImageId = image.Id },
                        cancellationToken: CancellationToken.None);

                // Assert
                Assert.Equal(image.Id, result.Metadata.Id);
                Assert.Equal(1, binaryStore.OpenReadCallCount);

                using MemoryStream copy = new();
                await result.Content.CopyToAsync(destination: copy, cancellationToken: CancellationToken.None);
                Assert.Equal(ImageUseCaseTestFixtures.VALID_PNG_PAYLOAD, copy.ToArray());

                await Assert.ThrowsAsync<KeyNotFoundException>(() => useCase.ExecuteAsync(
                        query: new GetImageStreamQuery { ImageId = metadataOnlyImage.Id },
                        cancellationToken: CancellationToken.None));
        }

        /// <summary>
        /// Verifies that <see cref="DeleteImageUseCase"/> blocks deletion when referenced by any template (including archived templates) and persists soft-deletion for unreferenced images.
        /// </summary>
        [Fact]
        public async Task DeleteImageUseCase_WhenReferencedVsUnreferenced_EnforcesReferentialIntegrityAsync()
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerTemplateRepository templateRepository = new(dbContext: dbContext);
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                ImageEntity referencedImage = ImageUseCaseTestFixtures.CreateSeededImage(payloadBytes: ImageUseCaseTestFixtures.VALID_PNG_PAYLOAD);
                ImageEntity freeImage = ImageUseCaseTestFixtures.CreateSeededImage(payloadBytes: ImageUseCaseTestFixtures.VALID_PNG_PAYLOAD);

                await imageRepository.AddAsync(
                        metadata: referencedImage,
                        binary: ImageBinaryEntity.Create(imageId: referencedImage.Id, data: ImageUseCaseTestFixtures.VALID_PNG_PAYLOAD),
                        cancellationToken: CancellationToken.None);
                await imageRepository.AddAsync(
                        metadata: freeImage,
                        binary: ImageBinaryEntity.Create(imageId: freeImage.Id, data: ImageUseCaseTestFixtures.VALID_PNG_PAYLOAD),
                        cancellationToken: CancellationToken.None);

                // Link referencedImage via an archived template in SQL Server to ensure archived templates still block image deletion.
                TemplateEntity archivedTemplate = TemplateEntity.Create(
                        storageNodeId: ImageUseCaseTestFixtures.VALID_NODE_ID,
                        name: "Archived Layout",
                        componentType: TemplateComponentType.InfoScreen,
                        schemaJson: $"{{\"hero\":\"{referencedImage.Reference}\"}}",
                        actorId: ImageUseCaseTestFixtures.VALID_ACTOR_ID);
                archivedTemplate.Archive(actorId: ImageUseCaseTestFixtures.VALID_ACTOR_ID);
                await templateRepository.AddAsync(template: archivedTemplate, cancellationToken: CancellationToken.None);

                DeleteImageUseCase useCase = new(imageRepository: imageRepository);

                // Act & Assert
                await Assert.ThrowsAsync<EntityInUseException>(() => useCase.ExecuteAsync(
                        command: new DeleteImageCommand { ImageId = referencedImage.Id, ActorId = ImageUseCaseTestFixtures.VALID_ACTOR_ID },
                        cancellationToken: CancellationToken.None));

                await useCase.ExecuteAsync(
                        command: new DeleteImageCommand { ImageId = freeImage.Id, ActorId = ImageUseCaseTestFixtures.VALID_ACTOR_ID },
                        cancellationToken: CancellationToken.None);

                ImageEntity? reloadedFreeImage = await imageRepository.GetByIdAsync(
                        id: freeImage.Id,
                        cancellationToken: CancellationToken.None);
                Assert.NotNull(reloadedFreeImage);
                Assert.True(reloadedFreeImage.IsDeleted);
        }

        /// <summary>
        /// Verifies that <see cref="ListImagesUseCase"/> clamps negative, zero, and <see cref="int.MaxValue"/> pagination values without overflow and excludes deleted rows in SQL Server.
        /// </summary>
        [Fact]
        public async Task ListImagesUseCase_WhenOutOfRangePagination_ClampsBoundsAndExcludesDeletedAsync()
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                ImageEntity activeImage = ImageUseCaseTestFixtures.CreateSeededImage(payloadBytes: ImageUseCaseTestFixtures.VALID_PNG_PAYLOAD);
                ImageEntity deletedImage = ImageUseCaseTestFixtures.CreateSeededImage(payloadBytes: ImageUseCaseTestFixtures.VALID_PNG_PAYLOAD);
                deletedImage.MarkDeleted(actorId: ImageUseCaseTestFixtures.VALID_ACTOR_ID);

                await imageRepository.AddAsync(
                        metadata: activeImage,
                        binary: ImageBinaryEntity.Create(imageId: activeImage.Id, data: ImageUseCaseTestFixtures.VALID_PNG_PAYLOAD),
                        cancellationToken: CancellationToken.None);
                await imageRepository.AddAsync(
                        metadata: deletedImage,
                        binary: ImageBinaryEntity.Create(imageId: deletedImage.Id, data: ImageUseCaseTestFixtures.VALID_PNG_PAYLOAD),
                        cancellationToken: CancellationToken.None);

                ListImagesUseCase useCase = new(imageRepository: imageRepository);

                // Act
                PagedResult<ImageMetadataResult> negativeResult = await useCase.ExecuteAsync(
                        query: new ListImagesQuery { Page = NEGATIVE_PAGE_NUMBER, PageSize = OVERSIZED_PAGE_SIZE },
                        cancellationToken: CancellationToken.None);
                PagedResult<ImageMetadataResult> maxPageResult = await useCase.ExecuteAsync(
                        query: new ListImagesQuery { Page = int.MaxValue, PageSize = 0 },
                        cancellationToken: CancellationToken.None);

                // Assert
                Assert.Equal(ApplicationConstants.DEFAULT_PAGE, negativeResult.Page);
                Assert.Equal(ApplicationConstants.MAX_PAGE_SIZE, negativeResult.PageSize);
                Assert.Equal(1, negativeResult.TotalCount);
                ImageMetadataResult returned = Assert.Single(negativeResult.Items);
                Assert.Equal(activeImage.Id, returned.Id);

                Assert.Equal(ApplicationConstants.MIN_PAGE_SIZE, maxPageResult.PageSize);
                Assert.Empty(maxPageResult.Items);
                Assert.Equal(1, maxPageResult.TotalPages);
        }
}
