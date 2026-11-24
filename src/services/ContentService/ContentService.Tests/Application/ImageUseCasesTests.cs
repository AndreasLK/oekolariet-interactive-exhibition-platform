using System.Security.Cryptography;
using System.Text;
using ContentService.Application.Common;
using ContentService.Application.Models;
using ContentService.Application.Options;
using ContentService.Application.UseCases.Images;
using ContentService.Application.Validation;
using ContentService.Domain.Common;
using ContentService.Domain.Entities;
using ContentService.Domain.Enums;
using ContentService.Domain.Exceptions;
using ContentService.Tests.Application.Persistence;
using MicrosoftOptions = Microsoft.Extensions.Options.Options;

namespace ContentService.Tests.Application;

/// <summary>
/// Integration tests for all image lifecycle use cases executed against a real SQL Server container.
/// </summary>
[Collection(MsSqlContentDatabaseCollection.COLLECTION_NAME)]
public sealed class ImageUseCasesTests : IAsyncLifetime
{
        /// <summary>
        /// Valid storage node identifier configured for test uploads.
        /// </summary>
        private const string VALID_NODE_ID = "node-1";

        /// <summary>
        /// Valid actor identifier recorded on created and deleted test images.
        /// </summary>
        private const string VALID_ACTOR_ID = "curator-1";

        /// <summary>
        /// Canonical PNG file name used across upload test commands.
        /// </summary>
        private const string VALID_FILE_NAME = "exhibit.png";

        /// <summary>
        /// Generous storage quota in bytes used for nominal image lifecycle tests.
        /// </summary>
        private const long DEFAULT_TEST_QUOTA_BYTES = 10_000L;

        /// <summary>
        /// Tight maximum image size limit in bytes used to test oversized stream rejection.
        /// </summary>
        private const long SMALL_MAX_IMAGE_BYTES = 64L;

        /// <summary>
        /// Spoofed declared content length under <see cref="SMALL_MAX_IMAGE_BYTES"/> for size-limit bypass tests.
        /// </summary>
        private const int SPOOFED_SMALL_LENGTH = 16;

        /// <summary>
        /// Spoofed declared length under the remaining 10-byte quota used to exercise post-read quota enforcement.
        /// </summary>
        private const long SPOOFED_UNDER_QUOTA_LENGTH_BYTES = 8L;

        /// <summary>
        /// Actual stream length exceeding <see cref="SMALL_MAX_IMAGE_BYTES"/> despite a smaller declared length.
        /// </summary>
        private const int OVERSIZED_ACTUAL_LENGTH = 128;

        /// <summary>
        /// Negative page index used to verify pagination lower-bound clamping.
        /// </summary>
        private const int NEGATIVE_PAGE_NUMBER = -5;

        /// <summary>
        /// Oversized page size used to verify pagination upper-bound clamping.
        /// </summary>
        private const int OVERSIZED_PAGE_SIZE = 500;

        /// <summary>
        /// Byte count of the pre-seeded image used in tight-quota tests.
        /// </summary>
        private const int SEED_TEN_BYTES_LENGTH = 10;

        /// <summary>
        /// Minimal valid PNG header plus trailing payload bytes used across upload and streaming tests.
        /// </summary>
        private static readonly byte[] VALID_PNG_PAYLOAD = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02, 0x03, 0x04];

        /// <summary>
        /// Shared SQL Server test container fixture.
        /// </summary>
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
        /// Verifies that a valid upload persists metadata and binary payload atomically in SQL Server and computes exact SHA-256.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task UploadImageUseCase_WhenValidPng_PersistsMetadataAndBinaryWithExactSha256Async()
        {
                // Arrange
                SqlServerImageRepository imageRepository = new(fixture: this._fixture);
                SqlServerStorageQuotaMonitor quotaMonitor = new(fixture: this._fixture);
                UploadImageUseCase useCase = CreateUploadUseCase(
                        imageRepository: imageRepository,
                        quotaMonitor: quotaMonitor);
                using MemoryStream stream = new(buffer: VALID_PNG_PAYLOAD, writable: false);
                UploadImageCommand command = new()
                {
                        FileName = VALID_FILE_NAME,
                        ContentType = DomainConstants.CONTENT_TYPE_PNG,
                        DeclaredLengthBytes = VALID_PNG_PAYLOAD.Length,
                        Content = stream,
                        ActorId = VALID_ACTOR_ID,
                        AltText = "Exhibition mascot"
                };
                string expectedHash = Convert.ToHexStringLower(SHA256.HashData(VALID_PNG_PAYLOAD));

                // Act
                ImageMetadataResult result = await useCase.ExecuteAsync(command: command, cancellationToken: CancellationToken.None);

                // Assert
                // Confirm both computed picture reference projections and raw SQL binary persistence match the uploaded stream.
                Assert.Equal(VALID_FILE_NAME, result.FileName);
                Assert.Equal(DomainConstants.CONTENT_TYPE_PNG, result.ContentType);
                Assert.Equal(VALID_PNG_PAYLOAD.Length, result.SizeBytes);
                Assert.Equal(expectedHash, result.Sha256Hash);
                Assert.Equal($"picture.{VALID_NODE_ID}.{result.Id:D}", result.PictureReference);
                Assert.Equal($"{VALID_NODE_ID}_{result.Id:D}", result.ExternalId);
                Assert.Equal("Exhibition mascot", result.AltText);
                Assert.Equal(1, await imageRepository.CountStoredImagesAsync(cancellationToken: CancellationToken.None));

                ImageBinaryEntity? storedBinary = await imageRepository.GetStoredBinaryAsync(
                        imageId: result.Id,
                        cancellationToken: CancellationToken.None);
                Assert.NotNull(storedBinary);
                Assert.Equal(VALID_PNG_PAYLOAD, storedBinary.Data);
        }

        /// <summary>
        /// Verifies that streaming more bytes than <see cref="ContentStorageOptions.MaxImageSizeBytes"/> throws <see cref="DomainValidationException"/> even when <see cref="UploadImageCommand.DeclaredLengthBytes"/> is smaller.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task UploadImageUseCase_WhenActualStreamExceedsMaxImageSizeDespiteSmallerDeclaredLength_ThrowsDomainValidationExceptionAsync()
        {
                // Arrange
                SqlServerImageRepository imageRepository = new(fixture: this._fixture);
                SqlServerStorageQuotaMonitor quotaMonitor = new(fixture: this._fixture);
                UploadImageUseCase useCase = CreateUploadUseCase(
                        imageRepository: imageRepository,
                        quotaMonitor: quotaMonitor,
                        maxImageSizeBytes: SMALL_MAX_IMAGE_BYTES);
                // Copy valid PNG header bytes into an oversized buffer so header validation alone cannot mask the true stream length.
                byte[] oversizedPayload = new byte[OVERSIZED_ACTUAL_LENGTH];
                Array.Copy(sourceArray: VALID_PNG_PAYLOAD, destinationArray: oversizedPayload, length: VALID_PNG_PAYLOAD.Length);
                using MemoryStream stream = new(buffer: oversizedPayload, writable: false);
                UploadImageCommand command = new()
                {
                        FileName = VALID_FILE_NAME,
                        ContentType = DomainConstants.CONTENT_TYPE_PNG,
                        DeclaredLengthBytes = SPOOFED_SMALL_LENGTH,
                        Content = stream,
                        ActorId = VALID_ACTOR_ID
                };

                // Act & Assert
                // Actual byte count (128) exceeds MaxImageSizeBytes (64) and must be aborted while reading despite spoofed DeclaredLengthBytes (16).
                await Assert.ThrowsAsync<DomainValidationException>(() =>
                        useCase.ExecuteAsync(command: command, cancellationToken: CancellationToken.None));
                Assert.Equal(0, await imageRepository.CountStoredImagesAsync(cancellationToken: CancellationToken.None));
        }

        /// <summary>
        /// Verifies that zero-byte streams, unreadable streams, and mid-read stream faults fail without persisting to SQL Server.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task UploadImageUseCase_WhenZeroBytesUnreadableOrStreamFaults_RejectsWithoutPersistingAsync()
        {
                // Arrange
                SqlServerImageRepository imageRepository = new(fixture: this._fixture);
                SqlServerStorageQuotaMonitor quotaMonitor = new(fixture: this._fixture);
                UploadImageUseCase useCase = CreateUploadUseCase(
                        imageRepository: imageRepository,
                        quotaMonitor: quotaMonitor);

                using MemoryStream emptyStream = new([]);
                using UnreadableMemoryStream unreadableStream = new(VALID_PNG_PAYLOAD);
                using FaultingStream faultingStream = new();

                UploadImageCommand emptyPayloadCommand = new()
                {
                        FileName = VALID_FILE_NAME,
                        ContentType = DomainConstants.CONTENT_TYPE_PNG,
                        DeclaredLengthBytes = VALID_PNG_PAYLOAD.Length,
                        Content = emptyStream,
                        ActorId = VALID_ACTOR_ID
                };
                UploadImageCommand unreadableCommand = emptyPayloadCommand with { Content = unreadableStream };
                UploadImageCommand faultingCommand = emptyPayloadCommand with { Content = faultingStream };

                // Act & Assert
                // Empty streams and unreadable streams fail validation, whereas mid-read stream faults propagate IOException cleanly without partial writes.
                await Assert.ThrowsAsync<DomainValidationException>(() =>
                        useCase.ExecuteAsync(command: emptyPayloadCommand, cancellationToken: CancellationToken.None));
                await Assert.ThrowsAsync<DomainValidationException>(() =>
                        useCase.ExecuteAsync(command: unreadableCommand, cancellationToken: CancellationToken.None));
                await Assert.ThrowsAsync<IOException>(() =>
                        useCase.ExecuteAsync(command: faultingCommand, cancellationToken: CancellationToken.None));
                Assert.Equal(0, await imageRepository.CountStoredImagesAsync(cancellationToken: CancellationToken.None));
        }

        /// <summary>
        /// Verifies that spoofed magic bytes (e.g., SVG declaring <c>image/png</c>) throw <see cref="DomainValidationException"/> without persisting.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task UploadImageUseCase_WhenMagicBytesDoNotMatchDeclaredContentType_ThrowsDomainValidationExceptionAsync()
        {
                // Arrange
                SqlServerImageRepository imageRepository = new(fixture: this._fixture);
                SqlServerStorageQuotaMonitor quotaMonitor = new(fixture: this._fixture);
                UploadImageUseCase useCase = CreateUploadUseCase(
                        imageRepository: imageRepository,
                        quotaMonitor: quotaMonitor);
                byte[] svgBytes = Encoding.UTF8.GetBytes("<svg><script>alert(1)</script></svg>");
                using MemoryStream stream = new(buffer: svgBytes, writable: false);
                UploadImageCommand command = new()
                {
                        FileName = VALID_FILE_NAME,
                        ContentType = DomainConstants.CONTENT_TYPE_PNG,
                        DeclaredLengthBytes = svgBytes.Length,
                        Content = stream,
                        ActorId = VALID_ACTOR_ID
                };

                // Act & Assert
                // Spoofing PNG ContentType on an SVG script payload must fail before anything reaches SQL Server.
                await Assert.ThrowsAsync<DomainValidationException>(() =>
                        useCase.ExecuteAsync(command: command, cancellationToken: CancellationToken.None));
                Assert.Equal(0, await imageRepository.CountStoredImagesAsync(cancellationToken: CancellationToken.None));
        }

        /// <summary>
        /// Verifies that exceeding real SQL Server quota (post-read spoofed length or zero quota) throws <see cref="StorageQuotaExceededException"/> without persisting.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task UploadImageUseCase_WhenQuotaExceededOrZero_ThrowsStorageQuotaExceededExceptionWithoutPersistingAsync()
        {
                // Arrange
                const long TIGHT_QUOTA_BYTES = 20L;
                SqlServerImageRepository imageRepository = new(fixture: this._fixture);
                SqlServerStorageQuotaMonitor quotaMonitor = new(fixture: this._fixture);

                // Seed a 10-byte image so SQL Server SUM(FileSizeBytes) reports 10 used bytes out of 20.
                byte[] existingBytes = new byte[SEED_TEN_BYTES_LENGTH];
                ImageEntity existingImage = CreateSeededImage(payloadBytes: existingBytes);
                ImageBinaryEntity existingBinary = ImageBinaryEntity.Create(imageId: existingImage.Id, data: existingBytes);
                await imageRepository.AddAsync(metadata: existingImage, binary: existingBinary, cancellationToken: CancellationToken.None);

                UploadImageUseCase tightQuotaUseCase = CreateUploadUseCase(
                        imageRepository: imageRepository,
                        quotaMonitor: quotaMonitor,
                        totalQuotaBytes: TIGHT_QUOTA_BYTES);

                byte[] largerPng = [.. VALID_PNG_PAYLOAD, 0xAA, 0xBB, 0xCC];
                using MemoryStream spoofedStream = new(buffer: largerPng, writable: false);
                UploadImageCommand spoofedCommand = new()
                {
                        FileName = VALID_FILE_NAME,
                        ContentType = DomainConstants.CONTENT_TYPE_PNG,
                        // Smaller than remaining 10 bytes to pass early check, while actual 15 bytes exceed remaining 10 bytes.
                        DeclaredLengthBytes = SPOOFED_UNDER_QUOTA_LENGTH_BYTES,
                        Content = spoofedStream,
                        ActorId = VALID_ACTOR_ID
                };

                UploadImageUseCase zeroQuotaUseCase = CreateUploadUseCase(
                        imageRepository: imageRepository,
                        quotaMonitor: quotaMonitor,
                        totalQuotaBytes: 0L);
                using MemoryStream validStream = new(buffer: VALID_PNG_PAYLOAD, writable: false);
                UploadImageCommand normalCommand = new()
                {
                        FileName = VALID_FILE_NAME,
                        ContentType = DomainConstants.CONTENT_TYPE_PNG,
                        DeclaredLengthBytes = VALID_PNG_PAYLOAD.Length,
                        Content = validStream,
                        ActorId = VALID_ACTOR_ID
                };

                // Act & Assert
                await Assert.ThrowsAsync<StorageQuotaExceededException>(() =>
                        tightQuotaUseCase.ExecuteAsync(command: spoofedCommand, cancellationToken: CancellationToken.None));
                StorageQuotaExceededException zeroQuotaException = await Assert.ThrowsAsync<StorageQuotaExceededException>(() =>
                        zeroQuotaUseCase.ExecuteAsync(command: normalCommand, cancellationToken: CancellationToken.None));

                Assert.Equal(0L, zeroQuotaException.QuotaBytes);
                Assert.Equal(1, await imageRepository.CountStoredImagesAsync(cancellationToken: CancellationToken.None));
        }

        /// <summary>
        /// Verifies that <see cref="GetImageMetadataUseCase"/> returns active metadata and throws <see cref="KeyNotFoundException"/> for soft-deleted or unknown images.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task GetImageMetadataUseCase_WhenActiveOrDeletedOrMissing_BehavesAsExpectedAsync()
        {
                // Arrange
                SqlServerImageRepository imageRepository = new(fixture: this._fixture);
                ImageEntity activeImage = CreateSeededImage(payloadBytes: VALID_PNG_PAYLOAD);
                ImageEntity deletedImage = CreateSeededImage(payloadBytes: VALID_PNG_PAYLOAD);
                deletedImage.MarkDeleted(actorId: VALID_ACTOR_ID);

                await imageRepository.AddAsync(
                        metadata: activeImage,
                        binary: ImageBinaryEntity.Create(imageId: activeImage.Id, data: VALID_PNG_PAYLOAD),
                        cancellationToken: CancellationToken.None);
                await imageRepository.AddAsync(
                        metadata: deletedImage,
                        binary: ImageBinaryEntity.Create(imageId: deletedImage.Id, data: VALID_PNG_PAYLOAD),
                        cancellationToken: CancellationToken.None);

                GetImageMetadataUseCase useCase = new(imageRepository: imageRepository);

                // Act
                ImageMetadataResult found = await useCase.ExecuteAsync(imageId: activeImage.Id, cancellationToken: CancellationToken.None);

                // Assert
                // Active metadata resolves, whereas empty IDs fail validation and soft-deleted or unknown IDs throw KeyNotFoundException.
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
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task GetImageStreamUseCase_WhenActiveOrMissingBinary_StreamsBytesOrThrowsKeyNotFoundAsync()
        {
                // Arrange
                SqlServerImageRepository imageRepository = new(fixture: this._fixture);
                SqlServerImageBinaryStore binaryStore = new(fixture: this._fixture);
                ImageEntity image = CreateSeededImage(payloadBytes: VALID_PNG_PAYLOAD);
                ImageBinaryEntity binary = ImageBinaryEntity.Create(imageId: image.Id, data: VALID_PNG_PAYLOAD);
                await imageRepository.AddAsync(metadata: image, binary: binary, cancellationToken: CancellationToken.None);

                // Seed an orphaned metadata row without ImageBinaries to exercise missing-stream KeyNotFoundException handling.
                ImageEntity metadataOnlyImage = CreateSeededImage(payloadBytes: VALID_PNG_PAYLOAD);
                await imageRepository.SeedImageMetadataOnlyAsync(image: metadataOnlyImage, cancellationToken: CancellationToken.None);

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
                Assert.Equal(VALID_PNG_PAYLOAD, copy.ToArray());

                await Assert.ThrowsAsync<KeyNotFoundException>(() => useCase.ExecuteAsync(
                        query: new GetImageStreamQuery { ImageId = metadataOnlyImage.Id },
                        cancellationToken: CancellationToken.None));
        }

        /// <summary>
        /// Verifies that <see cref="DeleteImageUseCase"/> blocks deletion when referenced by any template (including archived templates) and persists soft-deletion for unreferenced images.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task DeleteImageUseCase_WhenReferencedVsUnreferenced_EnforcesReferentialIntegrityAsync()
        {
                // Arrange
                SqlServerTemplateRepository templateRepository = new(fixture: this._fixture);
                SqlServerImageRepository imageRepository = new(fixture: this._fixture);
                ImageEntity referencedImage = CreateSeededImage(payloadBytes: VALID_PNG_PAYLOAD);
                ImageEntity freeImage = CreateSeededImage(payloadBytes: VALID_PNG_PAYLOAD);

                await imageRepository.AddAsync(
                        metadata: referencedImage,
                        binary: ImageBinaryEntity.Create(imageId: referencedImage.Id, data: VALID_PNG_PAYLOAD),
                        cancellationToken: CancellationToken.None);
                await imageRepository.AddAsync(
                        metadata: freeImage,
                        binary: ImageBinaryEntity.Create(imageId: freeImage.Id, data: VALID_PNG_PAYLOAD),
                        cancellationToken: CancellationToken.None);

                // Link referencedImage via an archived template in SQL Server to ensure archived templates still block image deletion.
                TemplateEntity archivedTemplate = TemplateEntity.Create(
                        storageNodeId: VALID_NODE_ID,
                        name: "Archived Layout",
                        componentType: TemplateComponentType.InfoScreen,
                        schemaJson: $"{{\"hero\":\"{referencedImage.Reference}\"}}",
                        actorId: VALID_ACTOR_ID);
                archivedTemplate.Archive(actorId: VALID_ACTOR_ID);
                await templateRepository.AddAsync(template: archivedTemplate, cancellationToken: CancellationToken.None);

                DeleteImageUseCase useCase = new(imageRepository: imageRepository);

                // Act & Assert
                await Assert.ThrowsAsync<EntityInUseException>(() => useCase.ExecuteAsync(
                        command: new DeleteImageCommand { ImageId = referencedImage.Id, ActorId = VALID_ACTOR_ID },
                        cancellationToken: CancellationToken.None));

                await useCase.ExecuteAsync(
                        command: new DeleteImageCommand { ImageId = freeImage.Id, ActorId = VALID_ACTOR_ID },
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
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task ListImagesUseCase_WhenOutOfRangePagination_ClampsBoundsAndExcludesDeletedAsync()
        {
                // Arrange
                SqlServerImageRepository imageRepository = new(fixture: this._fixture);
                ImageEntity activeImage = CreateSeededImage(payloadBytes: VALID_PNG_PAYLOAD);
                ImageEntity deletedImage = CreateSeededImage(payloadBytes: VALID_PNG_PAYLOAD);
                deletedImage.MarkDeleted(actorId: VALID_ACTOR_ID);

                await imageRepository.AddAsync(
                        metadata: activeImage,
                        binary: ImageBinaryEntity.Create(imageId: activeImage.Id, data: VALID_PNG_PAYLOAD),
                        cancellationToken: CancellationToken.None);
                await imageRepository.AddAsync(
                        metadata: deletedImage,
                        binary: ImageBinaryEntity.Create(imageId: deletedImage.Id, data: VALID_PNG_PAYLOAD),
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
                // Page=0/negative clamps to defaults while Page=int.MaxValue yields an empty page without SQL offset overflow.
                Assert.Equal(ApplicationConstants.DEFAULT_PAGE, negativeResult.Page);
                Assert.Equal(ApplicationConstants.MAX_PAGE_SIZE, negativeResult.PageSize);
                Assert.Equal(1, negativeResult.TotalCount);
                ImageMetadataResult returned = Assert.Single(negativeResult.Items);
                Assert.Equal(activeImage.Id, returned.Id);

                Assert.Equal(ApplicationConstants.MIN_PAGE_SIZE, maxPageResult.PageSize);
                Assert.Empty(maxPageResult.Items);
                Assert.Equal(1, maxPageResult.TotalPages);
        }

        /// <summary>
        /// Constructs an <see cref="UploadImageUseCase"/> wired to the real SQL Server repositories and validator.
        /// </summary>
        /// <param name="imageRepository">The SQL Server image repository.</param>
        /// <param name="quotaMonitor">The SQL Server storage quota monitor.</param>
        /// <param name="maxImageSizeBytes">Optional per-image size ceiling override.</param>
        /// <param name="totalQuotaBytes">Optional total node quota override.</param>
        /// <returns>A configured <see cref="UploadImageUseCase"/> instance.</returns>
        private static UploadImageUseCase CreateUploadUseCase(
                SqlServerImageRepository imageRepository,
                SqlServerStorageQuotaMonitor quotaMonitor,
                long maxImageSizeBytes = DomainConstants.MAX_IMAGE_SIZE_BYTES,
                long totalQuotaBytes = DEFAULT_TEST_QUOTA_BYTES)
        {
                ContentStorageOptions options = new()
                {
                        StorageNodeId = VALID_NODE_ID,
                        MaxImageSizeBytes = maxImageSizeBytes,
                        TotalQuotaBytes = totalQuotaBytes
                };

                return new UploadImageUseCase(
                        imageRepository: imageRepository,
                        quotaMonitor: quotaMonitor,
                        signatureValidator: new ImageHeaderSignatureValidator(),
                        options: MicrosoftOptions.Create(options));
        }

        /// <summary>
        /// Creates a valid <see cref="ImageEntity"/> metadata record whose SHA-256 hash matches <paramref name="payloadBytes"/>.
        /// </summary>
        /// <param name="payloadBytes">The raw image payload bytes.</param>
        /// <returns>A newly created <see cref="ImageEntity"/>.</returns>
        private static ImageEntity CreateSeededImage(byte[] payloadBytes)
        {
                string hash = Convert.ToHexStringLower(SHA256.HashData(payloadBytes));
                return ImageEntity.Create(
                        storageNodeId: VALID_NODE_ID,
                        fileName: VALID_FILE_NAME,
                        contentType: DomainConstants.CONTENT_TYPE_PNG,
                        fileSizeBytes: payloadBytes.Length,
                        sha256Hash: hash,
                        actorId: VALID_ACTOR_ID);
        }

        /// <summary>
        /// MemoryStream test double reporting <see cref="CanRead"/> as <see langword="false"/>.
        /// </summary>
        private sealed class UnreadableMemoryStream : MemoryStream
        {
                /// <summary>
                /// Initializes a new instance of the <see cref="UnreadableMemoryStream"/> class.
                /// </summary>
                /// <param name="buffer">The underlying byte buffer.</param>
                public UnreadableMemoryStream(byte[] buffer)
                        : base(buffer: buffer, writable: false)
                {
                }

                /// <inheritdoc />
                public override bool CanRead => false;
        }

        /// <summary>
        /// Readable stream test double that throws <see cref="IOException"/> mid-read.
        /// </summary>
        private sealed class FaultingStream : Stream
        {
                /// <inheritdoc />
                public override bool CanRead => true;

                /// <inheritdoc />
                public override bool CanSeek => false;

                /// <inheritdoc />
                public override bool CanWrite => false;

                /// <inheritdoc />
                public override long Length => throw new NotSupportedException();

                /// <inheritdoc />
                public override long Position
                {
                        get => throw new NotSupportedException();
                        set => throw new NotSupportedException();
                }

                /// <inheritdoc />
                public override void Flush()
                {
                }

                /// <inheritdoc />
                public override int Read(byte[] buffer, int offset, int count) => throw new IOException("Simulated read error.");

                /// <inheritdoc />
                public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
                        throw new IOException("Simulated async read error.");

                /// <inheritdoc />
                public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

                /// <inheritdoc />
                public override void SetLength(long value) => throw new NotSupportedException();

                /// <inheritdoc />
                public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
}
