using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using ContentService.Application.Common;
using ContentService.Application.Interfaces;
using ContentService.Application.Models;
using ContentService.Application.Options;
using ContentService.Application.UseCases.Health;
using ContentService.Application.UseCases.Images;
using ContentService.Application.UseCases.Publishing;
using ContentService.Application.Validation;
using ContentService.Domain.Common;
using ContentService.Domain.Entities;
using ContentService.Domain.Enums;
using ContentService.Domain.Exceptions;
using Microsoft.Extensions.Options;

namespace ContentService.Tests.Application;

/// <summary>
/// Fast, Docker-free unit tests covering Application edge cases after removing micro-optimizations.
/// </summary>
public sealed class ContentApplicationUnitTests
{
        /// <summary>
        /// Valid storage node identifier used across unit tests.
        /// </summary>
        private const string NODE_ID = "node-1";

        /// <summary>
        /// Valid actor identifier used across unit tests.
        /// </summary>
        private const string ACTOR_ID = "unit-tester";

        /// <summary>
        /// Small upload size cap used to test spoofed declared-length early abort.
        /// </summary>
        private const long SMALL_MAX_IMAGE_BYTES = 32L;

        /// <summary>
        /// Oversized non-seekable payload length exceeding <see cref="SMALL_MAX_IMAGE_BYTES"/>.
        /// </summary>
        private const int SPOOFED_ACTUAL_PAYLOAD_BYTES = 128;

        /// <summary>
        /// Valid 8-byte PNG signature prefix followed by minimal data.
        /// </summary>
        private static readonly byte[] VALID_PNG_BYTES = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01];

        /// <summary>
        /// Valid JPEG header bytes followed by minimal data.
        /// </summary>
        private static readonly byte[] VALID_JPEG_BYTES = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];

        /// <summary>
        /// Valid WebP header bytes followed by minimal data.
        /// </summary>
        private static readonly byte[] VALID_WEBP_BYTES = [0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50, 0x56];

        /// <summary>
        /// Verifies that <see cref="GetImageStreamUseCase"/> opens the binary stream once and returns matching metadata and bytes for an active image.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task GetImageStreamUseCase_WhenActiveImageExists_OpensBinaryStreamAndReturnsBytesAsync()
        {
                // Arrange
                FakeImageRepository imageRepository = new();
                SpyImageBinaryStore binaryStore = new(backingRepository: imageRepository);
                ImageEntity image = CreateImage(fileName: "hero.png", contentType: DomainConstants.CONTENT_TYPE_PNG, bytes: VALID_PNG_BYTES);
                imageRepository.Seed(image: image, binaryBytes: VALID_PNG_BYTES);

                GetImageStreamUseCase useCase = new(imageRepository: imageRepository, imageBinaryStore: binaryStore);

                // Act
                await using ImageStreamResult result = await useCase.ExecuteAsync(
                        query: new GetImageStreamQuery { ImageId = image.Id },
                        cancellationToken: CancellationToken.None);

                // Assert
                Assert.Equal(image.Id, result.Metadata.Id);
                Assert.Equal(1, binaryStore.OpenReadCalls);

                using MemoryStream copy = new();
                await result.Content.CopyToAsync(destination: copy, cancellationToken: CancellationToken.None);
                Assert.Equal(VALID_PNG_BYTES, copy.ToArray());
        }

        /// <summary>
        /// Verifies that <see cref="GetImageStreamUseCase"/> rejects empty IDs and throws <see cref="KeyNotFoundException"/> for soft-deleted or binary-less images.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task GetImageStreamUseCase_WhenEmptyDeletedOrOrphaned_ThrowsExpectedExceptionAsync()
        {
                // Arrange
                FakeImageRepository imageRepository = new();
                SpyImageBinaryStore binaryStore = new(backingRepository: imageRepository);
                ImageEntity deletedImage = CreateImage(fileName: "deleted.png", contentType: DomainConstants.CONTENT_TYPE_PNG, bytes: VALID_PNG_BYTES);
                deletedImage.MarkDeleted(actorId: ACTOR_ID);
                imageRepository.Seed(image: deletedImage, binaryBytes: VALID_PNG_BYTES);

                ImageEntity orphanedImage = CreateImage(fileName: "orphan.png", contentType: DomainConstants.CONTENT_TYPE_PNG, bytes: VALID_PNG_BYTES);
                imageRepository.Seed(image: orphanedImage, binaryBytes: null);

                GetImageStreamUseCase useCase = new(imageRepository: imageRepository, imageBinaryStore: binaryStore);

                // Act & Assert
                await Assert.ThrowsAsync<DomainValidationException>(() =>
                        useCase.ExecuteAsync(query: new GetImageStreamQuery { ImageId = Guid.Empty }, cancellationToken: CancellationToken.None));
                await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                        useCase.ExecuteAsync(query: new GetImageStreamQuery { ImageId = deletedImage.Id }, cancellationToken: CancellationToken.None));
                await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                        useCase.ExecuteAsync(query: new GetImageStreamQuery { ImageId = orphanedImage.Id }, cancellationToken: CancellationToken.None));
        }

        /// <summary>
        /// Verifies that <see cref="UploadImageUseCase"/> supports JPEG/WebP uploads and enforces spoofed declared-length limits and quota saturation safely.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task UploadImageUseCase_WhenJpegWebpSpoofedLengthOrQuotaSaturated_BehavesCorrectlyAsync()
        {
                // Arrange
                FakeImageRepository imageRepository = new();
                FixedQuotaMonitor quotaMonitor = new(usedBytes: 0L);
                ContentStorageOptions options = new()
                {
                        StorageNodeId = NODE_ID,
                        MaxImageSizeBytes = SMALL_MAX_IMAGE_BYTES,
                        TotalQuotaBytes = ApplicationConstants.DEFAULT_TOTAL_QUOTA_BYTES
                };
                UploadImageUseCase useCase = new(
                        imageRepository: imageRepository,
                        quotaMonitor: quotaMonitor,
                        signatureValidator: new ImageHeaderSignatureValidator(),
                        options: Options.Create(options));

                // Act: JPEG & WebP uploads succeed cleanly.
                using MemoryStream jpegStream = new(buffer: VALID_JPEG_BYTES, writable: false);
                ImageMetadataResult jpegResult = await useCase.ExecuteAsync(
                        command: new UploadImageCommand
                        {
                                FileName = "photo.jpg",
                                ContentType = DomainConstants.CONTENT_TYPE_JPEG,
                                DeclaredLengthBytes = VALID_JPEG_BYTES.Length,
                                Content = jpegStream,
                                ActorId = ACTOR_ID
                        },
                        cancellationToken: CancellationToken.None);

                using MemoryStream webpStream = new(buffer: VALID_WEBP_BYTES, writable: false);
                ImageMetadataResult webpResult = await useCase.ExecuteAsync(
                        command: new UploadImageCommand
                        {
                                FileName = "sticker.webp",
                                ContentType = DomainConstants.CONTENT_TYPE_WEBP,
                                DeclaredLengthBytes = VALID_WEBP_BYTES.Length,
                                Content = webpStream,
                                ActorId = ACTOR_ID
                        },
                        cancellationToken: CancellationToken.None);

                // Act & Assert: Non-seekable stream claiming 16 bytes while delivering 128 bytes must abort with DomainValidationException.
                using NonSeekableChunkStream spoofedStream = new(totalBytes: SPOOFED_ACTUAL_PAYLOAD_BYTES);
                await Assert.ThrowsAsync<DomainValidationException>(() => useCase.ExecuteAsync(
                        command: new UploadImageCommand
                        {
                                FileName = "spoofed.png",
                                ContentType = DomainConstants.CONTENT_TYPE_PNG,
                                DeclaredLengthBytes = 16L,
                                Content = spoofedStream,
                                ActorId = ACTOR_ID
                        },
                        cancellationToken: CancellationToken.None));

                Assert.Equal(DomainConstants.CONTENT_TYPE_JPEG, jpegResult.ContentType);
                Assert.Equal(DomainConstants.CONTENT_TYPE_WEBP, webpResult.ContentType);

                // Verify long.MaxValue quota saturation does not overflow to negative numbers.
                FixedQuotaMonitor saturatedQuota = new(usedBytes: long.MaxValue - 4L);
                ContentStorageOptions smallQuotaOptions = new()
                {
                        StorageNodeId = NODE_ID,
                        MaxImageSizeBytes = SMALL_MAX_IMAGE_BYTES,
                        TotalQuotaBytes = long.MaxValue - 2L
                };
                UploadImageUseCase saturatedUseCase = new(
                        imageRepository: imageRepository,
                        quotaMonitor: saturatedQuota,
                        signatureValidator: new ImageHeaderSignatureValidator(),
                        options: Options.Create(smallQuotaOptions));
                using MemoryStream smallPng = new(buffer: VALID_PNG_BYTES, writable: false);
                StorageQuotaExceededException quotaException = await Assert.ThrowsAsync<StorageQuotaExceededException>(() =>
                        saturatedUseCase.ExecuteAsync(
                                command: new UploadImageCommand
                                {
                                        FileName = "overflow.png",
                                        ContentType = DomainConstants.CONTENT_TYPE_PNG,
                                        DeclaredLengthBytes = VALID_PNG_BYTES.Length,
                                        Content = smallPng,
                                        ActorId = ACTOR_ID
                                },
                                cancellationToken: CancellationToken.None));
                Assert.Equal(long.MaxValue, quotaException.UsedBytes);
        }

        /// <summary>
        /// Verifies that <see cref="ResolvePublishBundleUseCase"/> produces the exact big-endian length-prefixed SHA-256 digest expected by offline kiosks.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task ResolvePublishBundleUseCase_WhenBundlingKnownInputs_MatchesCanonicalBigEndianSha256VectorAsync()
        {
                // Arrange
                FakeImageRepository imageRepository = new();
                FakeTemplateRepository templateRepository = new();
                ImageEntity asset = CreateImage(fileName: "asset.png", contentType: DomainConstants.CONTENT_TYPE_PNG, bytes: VALID_PNG_BYTES);
                imageRepository.Seed(image: asset, binaryBytes: VALID_PNG_BYTES);

                TemplateEntity template = TemplateEntity.Create(
                        storageNodeId: NODE_ID,
                        name: "Canonical Template",
                        componentType: TemplateComponentType.InfoScreen,
                        schemaJson: "{\"title\":\"Eco\"}",
                        actorId: ACTOR_ID);
                templateRepository.Seed(template: template);

                string implementationJson = $"{{\"hero\":\"{asset.Reference}\"}}";
                ResolvePublishBundleUseCase useCase = new(templateRepository: templateRepository, imageRepository: imageRepository);

                // Compute expected canonical digest independently using big-endian length prefixes.
                using IncrementalHash expectedHasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                AppendReferenceSegment(hasher: expectedHasher, text: template.SchemaJson);
                AppendReferenceSegment(hasher: expectedHasher, text: implementationJson);
                AppendReferenceSegment(
                        hasher: expectedHasher,
                        text: $"{asset.StorageNodeId}:{asset.Id.ToString(DomainConstants.GUID_FORMAT_HYPHENATED)}:{asset.Sha256Hash}");
                string expectedChecksum = Convert.ToHexStringLower(expectedHasher.GetHashAndReset());

                // Act
                PublishBundleResult bundle = await useCase.ExecuteAsync(
                        command: new ResolvePublishBundleCommand
                        {
                                TemplateId = template.Id,
                                ImplementationJson = implementationJson
                        },
                        cancellationToken: CancellationToken.None);

                // Assert
                Assert.Equal(expectedChecksum, bundle.BundleChecksum);
                BundleAssetResult bundledAsset = Assert.Single(bundle.Assets);
                Assert.Equal(asset.Id, bundledAsset.ImageId);
        }

        /// <summary>
        /// Verifies defensive clamping when an <see cref="IStorageQuotaMonitor"/> implementation returns a negative byte count.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task GetStorageHealthUseCase_WhenMonitorReportsNegativeUsage_ClampsUsedBytesToZeroAsync()
        {
                // Arrange
                // Inject a faulty monitor returning negative bytes to verify defensive clamping without touching SQL Server.
                const long NEGATIVE_USAGE_BYTES = -10L;
                const long QUOTA_BYTES = 1000L;
                FixedQuotaMonitor negativeMonitor = new(usedBytes: NEGATIVE_USAGE_BYTES);
                ContentStorageOptions options = new() { TotalQuotaBytes = QUOTA_BYTES };
                GetStorageHealthUseCase useCase = new(
                        quotaMonitor: negativeMonitor,
                        options: Options.Create(options));

                // Act
                StorageHealthResult result = await useCase.ExecuteAsync(cancellationToken: CancellationToken.None);

                // Assert
                Assert.Equal(StorageHealthStatus.Healthy, result.Status);
                Assert.Equal(0L, result.UsedBytes);
                Assert.Equal(0.0, result.UtilizationPercent);
        }

        /// <summary>
        /// Verifies that <see cref="ContentStorageOptionsValidator"/> rejects null options, invalid storage node IDs, bounds, and threshold orderings.
        /// </summary>
        [Fact]
        public void ContentStorageOptionsValidator_WhenOptionsInvalidOrValid_ReturnsExpectedValidationResult()
        {
                // Arrange
                ContentStorageOptionsValidator validator = new();

                // Assert valid defaults pass.
                Assert.True(validator.Validate(name: null, options: new ContentStorageOptions()).Succeeded);

                // Assert null options, invalid storage node IDs, size limits, negative quota, and threshold configurations fail (0 quota is valid read-only mode).
                Assert.True(validator.Validate(name: null, options: null!).Failed);
                Assert.True(validator.Validate(name: null, options: new ContentStorageOptions { TotalQuotaBytes = 0L }).Succeeded);
                Assert.True(validator.Validate(name: null, options: new ContentStorageOptions { StorageNodeId = "bad node!" }).Failed);
                Assert.True(validator.Validate(name: null, options: new ContentStorageOptions { StorageNodeId = "Node_1" }).Failed);
                Assert.True(validator.Validate(name: null, options: new ContentStorageOptions { MaxImageSizeBytes = 0L }).Failed);
                Assert.True(validator.Validate(name: null, options: new ContentStorageOptions { MaxImageSizeBytes = DomainConstants.MAX_IMAGE_SIZE_BYTES + 1L }).Failed);
                Assert.True(validator.Validate(name: null, options: new ContentStorageOptions { TotalQuotaBytes = -1L }).Failed);
                Assert.True(validator.Validate(name: null, options: new ContentStorageOptions { WarningThresholdPercent = 95.0, CriticalThresholdPercent = 95.0 }).Failed);
                Assert.True(validator.Validate(name: null, options: new ContentStorageOptions { WarningThresholdPercent = 95.0, CriticalThresholdPercent = 80.0 }).Failed);
                Assert.True(validator.Validate(name: null, options: new ContentStorageOptions { WarningThresholdPercent = double.NaN }).Failed);
                Assert.True(validator.Validate(name: null, options: new ContentStorageOptions { CriticalThresholdPercent = 101.0 }).Failed);
        }

        /// <summary>
        /// Appends a big-endian length-prefixed UTF-8 segment to the reference SHA-256 accumulator.
        /// </summary>
        /// <param name="hasher">The reference hash accumulator.</param>
        /// <param name="text">The segment text.</param>
        private static void AppendReferenceSegment(IncrementalHash hasher, string text)
        {
                byte[] utf8 = Encoding.UTF8.GetBytes(text);
                byte[] prefix = new byte[sizeof(int)];
                BinaryPrimitives.WriteInt32BigEndian(destination: prefix, value: utf8.Length);
                hasher.AppendData(prefix);
                hasher.AppendData(utf8);
        }

        /// <summary>
        /// Creates a deterministic <see cref="ImageEntity"/> for unit testing.
        /// </summary>
        /// <param name="fileName">The test file name.</param>
        /// <param name="contentType">The MIME content type.</param>
        /// <param name="bytes">The payload bytes.</param>
        /// <returns>A newly initialized <see cref="ImageEntity"/>.</returns>
        private static ImageEntity CreateImage(string fileName, string contentType, byte[] bytes)
        {
                string hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
                return ImageEntity.Create(
                        storageNodeId: NODE_ID,
                        fileName: fileName,
                        contentType: contentType,
                        fileSizeBytes: bytes.Length,
                        sha256Hash: hash,
                        actorId: ACTOR_ID);
        }

        /// <summary>
        /// Minimal in-memory <see cref="IImageRepository"/> test double for unit tests.
        /// </summary>
        private sealed class FakeImageRepository : IImageRepository
        {
                private readonly Dictionary<Guid, ImageEntity> _images = [];
                private readonly Dictionary<Guid, byte[]> _binaries = [];

                public void Seed(ImageEntity image, byte[]? binaryBytes)
                {
                        this._images[image.Id] = image;
                        if (binaryBytes is not null)
                        {
                                this._binaries[image.Id] = binaryBytes;
                        }
                }

                public byte[]? GetBinary(Guid imageId) =>
                        this._binaries.TryGetValue(key: imageId, value: out byte[]? data) ? data : null;

                public Task<ImageEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
                        Task.FromResult(this._images.TryGetValue(key: id, value: out ImageEntity? image) ? image : null);

                public Task<IReadOnlyList<ImageEntity>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
                        Task.FromResult<IReadOnlyList<ImageEntity>>(
                                ids.Where(id => this._images.ContainsKey(id)).Select(id => this._images[id]).ToList());

                public Task<ImageEntity?> GetBySha256Async(string sha256Hash, CancellationToken cancellationToken) =>
                        Task.FromResult(this._images.Values.FirstOrDefault(image => !image.IsDeleted && image.Sha256Hash == sha256Hash));

                public Task<PagedResult<ImageEntity>> ListAsync(int page, int pageSize, string? searchTerm, CancellationToken cancellationToken) =>
                        Task.FromResult(new PagedResult<ImageEntity>
                        {
                                Items = this._images.Values.Where(image => !image.IsDeleted).ToList(),
                                Page = page,
                                PageSize = pageSize,
                                TotalCount = this._images.Values.Count(image => !image.IsDeleted)
                        });

                public Task<bool> IsReferencedByAnyTemplateAsync(Guid imageId, CancellationToken cancellationToken) =>
                        Task.FromResult(false);

                public Task AddAsync(ImageEntity metadata, ImageBinaryEntity binary, CancellationToken cancellationToken)
                {
                        this._images[metadata.Id] = metadata;
                        this._binaries[metadata.Id] = binary.Data;
                        return Task.CompletedTask;
                }

                public Task DeleteAsync(ImageEntity image, Guid expectedConcurrencyStamp, CancellationToken cancellationToken) =>
                        Task.CompletedTask;
        }

        /// <summary>
        /// Spy <see cref="IImageBinaryStore"/> counting <see cref="OpenReadAsync"/> invocations.
        /// </summary>
        private sealed class SpyImageBinaryStore : IImageBinaryStore
        {
                private readonly FakeImageRepository? _backingRepository;

                public SpyImageBinaryStore(FakeImageRepository? backingRepository = null)
                {
                        this._backingRepository = backingRepository;
                }

                public int OpenReadCalls { get; private set; }

                public Task<Stream?> OpenReadAsync(Guid imageId, CancellationToken cancellationToken)
                {
                        this.OpenReadCalls++;
                        byte[]? data = this._backingRepository?.GetBinary(imageId: imageId);
                        Stream? stream = data is null ? null : new MemoryStream(buffer: data, writable: false);
                        return Task.FromResult(stream);
                }
        }

        /// <summary>
        /// Minimal in-memory <see cref="ITemplateRepository"/> test double.
        /// </summary>
        private sealed class FakeTemplateRepository : ITemplateRepository
        {
                private readonly Dictionary<Guid, TemplateEntity> _templates = [];

                public void Seed(TemplateEntity template) => this._templates[template.Id] = template;

                public Task<TemplateEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
                        Task.FromResult(this._templates.TryGetValue(key: id, value: out TemplateEntity? template) ? template : null);

                public Task<bool> ExistsByNameAsync(string name, Guid? excludeTemplateId, CancellationToken cancellationToken) =>
                        Task.FromResult(false);

                public Task<PagedResult<TemplateEntity>> ListAsync(
                        int page,
                        int pageSize,
                        TemplateComponentType? componentType,
                        string? searchTerm,
                        CancellationToken cancellationToken) =>
                        Task.FromResult(new PagedResult<TemplateEntity>
                        {
                                Items = this._templates.Values.ToList(),
                                Page = page,
                                PageSize = pageSize,
                                TotalCount = this._templates.Count
                        });

                public Task AddAsync(TemplateEntity template, CancellationToken cancellationToken)
                {
                        this._templates[template.Id] = template;
                        return Task.CompletedTask;
                }

                public Task UpdateAsync(TemplateEntity template, Guid expectedConcurrencyStamp, CancellationToken cancellationToken) =>
                        Task.CompletedTask;

                public Task DeleteAsync(TemplateEntity template, Guid expectedConcurrencyStamp, CancellationToken cancellationToken) =>
                        Task.CompletedTask;
        }

        /// <summary>
        /// Fixed quota monitor returning a constant byte usage value.
        /// </summary>
        private sealed class FixedQuotaMonitor : IStorageQuotaMonitor
        {
                private readonly long _usedBytes;

                public FixedQuotaMonitor(long usedBytes) => this._usedBytes = usedBytes;

                public Task<long> GetUsedBytesAsync(CancellationToken cancellationToken) => Task.FromResult(this._usedBytes);
        }

        /// <summary>
        /// Non-seekable stream emitting deterministic bytes in small chunks to verify streaming overflow protection.
        /// </summary>
        private sealed class NonSeekableChunkStream : Stream
        {
                private readonly int _totalBytes;
                private int _bytesEmitted;

                public NonSeekableChunkStream(int totalBytes) => this._totalBytes = totalBytes;

                public override bool CanRead => true;

                public override bool CanSeek => false;

                public override bool CanWrite => false;

                public override long Length => throw new NotSupportedException();

                public override long Position
                {
                        get => throw new NotSupportedException();
                        set => throw new NotSupportedException();
                }

                public override void Flush()
                {
                }

                public override int Read(byte[] buffer, int offset, int count)
                {
                        int remaining = this._totalBytes - this._bytesEmitted;
                        if (remaining <= 0)
                        {
                                return 0;
                        }

                        int toCopy = Math.Min(val1: count, val2: remaining);
                        Array.Fill(array: buffer, value: (byte)0x89, startIndex: offset, count: toCopy);
                        this._bytesEmitted += toCopy;
                        return toCopy;
                }

                public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

                public override void SetLength(long value) => throw new NotSupportedException();

                public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
}
