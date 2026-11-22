using System.Security.Cryptography;
using System.Text;
using ContentService.Application.Models;
using ContentService.Application.Options;
using ContentService.Application.UseCases.Images;
using ContentService.Domain.Common;
using ContentService.Domain.Entities;
using ContentService.Domain.Exceptions;
using ContentService.Infrastructure.Persistence;
using ContentService.Infrastructure.Repositories;
using ContentService.Tests.Application.Persistence;
using ContentService.Tests.TestDoubles;

namespace ContentService.Tests.Application;

/// <summary>
/// Integration tests for <see cref="UploadImageUseCase"/> executed against a real SQL Server container.
/// </summary>
[Collection(MsSqlContentDatabaseCollection.COLLECTION_NAME)]
public sealed class UploadImageUseCaseTests : IAsyncLifetime
{
        private const long SMALL_MAX_IMAGE_BYTES = 64L;
        private const int SPOOFED_SMALL_LENGTH = 16;
        private const long SPOOFED_UNDER_QUOTA_LENGTH_BYTES = 8L;
        private const int OVERSIZED_ACTUAL_LENGTH = 128;
        private const int SEED_TEN_BYTES_LENGTH = 10;

        private readonly MsSqlContentDatabaseFixture _fixture;

        /// <summary>
        /// Initializes a new instance of the <see cref="UploadImageUseCaseTests"/> class.
        /// </summary>
        /// <param name="fixture">The shared SQL Server container fixture.</param>
        public UploadImageUseCaseTests(MsSqlContentDatabaseFixture fixture)
        {
                ArgumentNullException.ThrowIfNull(fixture);
                this._fixture = fixture;
        }

        /// <inheritdoc />
        public Task InitializeAsync() => this._fixture.ResetDatabaseAsync();

        /// <inheritdoc />
        public Task DisposeAsync() => Task.CompletedTask;

        /// <summary>
        /// Verifies that a valid upload persists metadata and binary payload atomically in SQL Server and computes exact SHA-256.
        /// </summary>
        [Fact]
        public async Task UploadImageUseCase_WhenValidPng_PersistsMetadataAndBinaryWithExactSha256Async()
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                SqlServerStorageQuotaMonitor quotaMonitor = new(dbContext: dbContext);
                UploadImageUseCase useCase = ImageUseCaseTestHelpers.CreateUploadUseCase(
                        imageRepository: imageRepository,
                        quotaMonitor: quotaMonitor);
                using MemoryStream stream = new(buffer: ImageUseCaseTestHelpers.VALID_PNG_PAYLOAD, writable: false);
                UploadImageCommand command = new()
                {
                        FileName = ImageUseCaseTestHelpers.VALID_FILE_NAME,
                        ContentType = DomainConstants.CONTENT_TYPE_PNG,
                        DeclaredLengthBytes = ImageUseCaseTestHelpers.VALID_PNG_PAYLOAD.Length,
                        Content = stream,
                        ActorId = ImageUseCaseTestHelpers.VALID_ACTOR_ID,
                        AltText = "Exhibition mascot"
                };
                string expectedHash = Convert.ToHexStringLower(SHA256.HashData(ImageUseCaseTestHelpers.VALID_PNG_PAYLOAD));

                // Act
                ImageMetadataResult result = await useCase.ExecuteAsync(command: command, cancellationToken: CancellationToken.None);

                // Assert
                Assert.Equal(ImageUseCaseTestHelpers.VALID_FILE_NAME, result.FileName);
                Assert.Equal(DomainConstants.CONTENT_TYPE_PNG, result.ContentType);
                Assert.Equal(ImageUseCaseTestHelpers.VALID_PNG_PAYLOAD.Length, result.SizeBytes);
                Assert.Equal(expectedHash, result.Sha256Hash);
                Assert.Equal($"picture.{ImageUseCaseTestHelpers.VALID_NODE_ID}.{result.Id:D}", result.PictureReference);
                Assert.Equal($"{ImageUseCaseTestHelpers.VALID_NODE_ID}_{result.Id:D}", result.ExternalId);
                Assert.Equal("Exhibition mascot", result.AltText);
                Assert.Equal(1, await this._fixture.CountStoredImagesAsync(cancellationToken: CancellationToken.None));

                ImageBinaryEntity? storedBinary = await this._fixture.GetStoredBinaryAsync(
                        imageId: result.Id,
                        cancellationToken: CancellationToken.None);
                Assert.NotNull(storedBinary);
                Assert.Equal(ImageUseCaseTestHelpers.VALID_PNG_PAYLOAD, storedBinary.Data);
        }

        /// <summary>
        /// Verifies that streaming more bytes than <see cref="ContentStorageOptions.MaxImageSizeBytes"/> throws <see cref="DomainValidationException"/> even when <see cref="UploadImageCommand.DeclaredLengthBytes"/> is smaller.
        /// </summary>
        [Fact]
        public async Task UploadImageUseCase_WhenActualStreamExceedsMaxImageSizeDespiteSmallerDeclaredLength_ThrowsDomainValidationExceptionAsync()
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                SqlServerStorageQuotaMonitor quotaMonitor = new(dbContext: dbContext);
                UploadImageUseCase useCase = ImageUseCaseTestHelpers.CreateUploadUseCase(
                        imageRepository: imageRepository,
                        quotaMonitor: quotaMonitor,
                        maxImageSizeBytes: SMALL_MAX_IMAGE_BYTES);
                byte[] oversizedPayload = new byte[OVERSIZED_ACTUAL_LENGTH];
                Array.Copy(
                        sourceArray: ImageUseCaseTestHelpers.VALID_PNG_PAYLOAD,
                        destinationArray: oversizedPayload,
                        length: ImageUseCaseTestHelpers.VALID_PNG_PAYLOAD.Length);
                using MemoryStream stream = new(buffer: oversizedPayload, writable: false);
                UploadImageCommand command = new()
                {
                        FileName = ImageUseCaseTestHelpers.VALID_FILE_NAME,
                        ContentType = DomainConstants.CONTENT_TYPE_PNG,
                        DeclaredLengthBytes = SPOOFED_SMALL_LENGTH,
                        Content = stream,
                        ActorId = ImageUseCaseTestHelpers.VALID_ACTOR_ID
                };

                // Act & Assert
                await Assert.ThrowsAsync<DomainValidationException>(() =>
                        useCase.ExecuteAsync(command: command, cancellationToken: CancellationToken.None));
                Assert.Equal(0, await this._fixture.CountStoredImagesAsync(cancellationToken: CancellationToken.None));
        }

        /// <summary>
        /// Verifies that zero-byte streams, unreadable streams, and mid-read stream faults fail without persisting to SQL Server.
        /// </summary>
        [Fact]
        public async Task UploadImageUseCase_WhenZeroBytesUnreadableOrStreamFaults_RejectsWithoutPersistingAsync()
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                SqlServerStorageQuotaMonitor quotaMonitor = new(dbContext: dbContext);
                UploadImageUseCase useCase = ImageUseCaseTestHelpers.CreateUploadUseCase(
                        imageRepository: imageRepository,
                        quotaMonitor: quotaMonitor);

                using MemoryStream emptyStream = new([]);
                using UnreadableMemoryStream unreadableStream = new(buffer: ImageUseCaseTestHelpers.VALID_PNG_PAYLOAD);
                using FaultingStream faultingStream = new();

                UploadImageCommand emptyPayloadCommand = new()
                {
                        FileName = ImageUseCaseTestHelpers.VALID_FILE_NAME,
                        ContentType = DomainConstants.CONTENT_TYPE_PNG,
                        DeclaredLengthBytes = ImageUseCaseTestHelpers.VALID_PNG_PAYLOAD.Length,
                        Content = emptyStream,
                        ActorId = ImageUseCaseTestHelpers.VALID_ACTOR_ID
                };
                UploadImageCommand unreadableCommand = emptyPayloadCommand with { Content = unreadableStream };
                UploadImageCommand faultingCommand = emptyPayloadCommand with { Content = faultingStream };

                // Act & Assert
                await Assert.ThrowsAsync<DomainValidationException>(() =>
                        useCase.ExecuteAsync(command: emptyPayloadCommand, cancellationToken: CancellationToken.None));
                await Assert.ThrowsAsync<DomainValidationException>(() =>
                        useCase.ExecuteAsync(command: unreadableCommand, cancellationToken: CancellationToken.None));
                await Assert.ThrowsAsync<IOException>(() =>
                        useCase.ExecuteAsync(command: faultingCommand, cancellationToken: CancellationToken.None));
                Assert.Equal(0, await this._fixture.CountStoredImagesAsync(cancellationToken: CancellationToken.None));
        }

        /// <summary>
        /// Verifies that spoofed file headers (e.g., SVG declaring <c>image/png</c>) throw <see cref="DomainValidationException"/> without persisting.
        /// </summary>
        [Fact]
        public async Task UploadImageUseCase_WhenFileHeaderDoesNotMatchDeclaredContentType_ThrowsDomainValidationExceptionAsync()
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                SqlServerStorageQuotaMonitor quotaMonitor = new(dbContext: dbContext);
                UploadImageUseCase useCase = ImageUseCaseTestHelpers.CreateUploadUseCase(
                        imageRepository: imageRepository,
                        quotaMonitor: quotaMonitor);
                byte[] svgBytes = Encoding.UTF8.GetBytes("<svg><script>alert(1)</script></svg>");
                using MemoryStream stream = new(buffer: svgBytes, writable: false);
                UploadImageCommand command = new()
                {
                        FileName = ImageUseCaseTestHelpers.VALID_FILE_NAME,
                        ContentType = DomainConstants.CONTENT_TYPE_PNG,
                        DeclaredLengthBytes = svgBytes.Length,
                        Content = stream,
                        ActorId = ImageUseCaseTestHelpers.VALID_ACTOR_ID
                };

                // Act & Assert
                await Assert.ThrowsAsync<DomainValidationException>(() =>
                        useCase.ExecuteAsync(command: command, cancellationToken: CancellationToken.None));
                Assert.Equal(0, await this._fixture.CountStoredImagesAsync(cancellationToken: CancellationToken.None));
        }

        /// <summary>
        /// Verifies that exceeding real SQL Server quota (post-read spoofed length or zero quota) throws <see cref="StorageQuotaExceededException"/> without persisting.
        /// </summary>
        [Fact]
        public async Task UploadImageUseCase_WhenQuotaExceededOrZero_ThrowsStorageQuotaExceededExceptionWithoutPersistingAsync()
        {
                // Arrange
                const long TIGHT_QUOTA_BYTES = 20L;
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                SqlServerStorageQuotaMonitor quotaMonitor = new(dbContext: dbContext);

                byte[] existingBytes = new byte[SEED_TEN_BYTES_LENGTH];
                ImageEntity existingImage = ImageUseCaseTestHelpers.CreateSeededImage(payloadBytes: existingBytes);
                ImageBinaryEntity existingBinary = ImageBinaryEntity.Create(imageId: existingImage.Id, data: existingBytes);
                await imageRepository.AddAsync(metadata: existingImage, binary: existingBinary, cancellationToken: CancellationToken.None);

                UploadImageUseCase tightQuotaUseCase = ImageUseCaseTestHelpers.CreateUploadUseCase(
                        imageRepository: imageRepository,
                        quotaMonitor: quotaMonitor,
                        totalQuotaBytes: TIGHT_QUOTA_BYTES);

                byte[] largerPng = [.. ImageUseCaseTestHelpers.VALID_PNG_PAYLOAD, 0xAA, 0xBB, 0xCC];
                using MemoryStream spoofedStream = new(buffer: largerPng, writable: false);
                UploadImageCommand spoofedCommand = new()
                {
                        FileName = ImageUseCaseTestHelpers.VALID_FILE_NAME,
                        ContentType = DomainConstants.CONTENT_TYPE_PNG,
                        DeclaredLengthBytes = SPOOFED_UNDER_QUOTA_LENGTH_BYTES,
                        Content = spoofedStream,
                        ActorId = ImageUseCaseTestHelpers.VALID_ACTOR_ID
                };

                UploadImageUseCase zeroQuotaUseCase = ImageUseCaseTestHelpers.CreateUploadUseCase(
                        imageRepository: imageRepository,
                        quotaMonitor: quotaMonitor,
                        totalQuotaBytes: 0L);
                using MemoryStream validStream = new(buffer: ImageUseCaseTestHelpers.VALID_PNG_PAYLOAD, writable: false);
                UploadImageCommand normalCommand = new()
                {
                        FileName = ImageUseCaseTestHelpers.VALID_FILE_NAME,
                        ContentType = DomainConstants.CONTENT_TYPE_PNG,
                        DeclaredLengthBytes = ImageUseCaseTestHelpers.VALID_PNG_PAYLOAD.Length,
                        Content = validStream,
                        ActorId = ImageUseCaseTestHelpers.VALID_ACTOR_ID
                };

                // Act & Assert
                await Assert.ThrowsAsync<StorageQuotaExceededException>(() =>
                        tightQuotaUseCase.ExecuteAsync(command: spoofedCommand, cancellationToken: CancellationToken.None));
                StorageQuotaExceededException zeroQuotaException = await Assert.ThrowsAsync<StorageQuotaExceededException>(() =>
                        zeroQuotaUseCase.ExecuteAsync(command: normalCommand, cancellationToken: CancellationToken.None));

                Assert.Equal(0L, zeroQuotaException.QuotaBytes);
                Assert.Equal(1, await this._fixture.CountStoredImagesAsync(cancellationToken: CancellationToken.None));
        }
}

