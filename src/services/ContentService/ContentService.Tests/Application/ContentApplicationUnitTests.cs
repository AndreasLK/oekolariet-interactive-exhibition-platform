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
using ContentService.Tests.TestDoubles;
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
        /// Official 8-byte PNG magic header (<c>0x89</c> + <c>"PNG"</c> + <c>CRLF</c> + <c>Ctrl+Z</c> + <c>LF</c>) plus 1 dummy byte (<c>0x01</c>)
        /// so mocked uploads pass magic-byte validation.
        /// </summary>
        private static readonly byte[] VALID_PNG_BYTES = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01];

        /// <summary>
        /// Official JPEG Start-of-Image (<c>0xFF 0xD8</c>) + APP0/JFIF marker (<c>0xFF 0xE0</c>) + segment length (<c>0x00 0x10</c>).
        /// </summary>
        private static readonly byte[] VALID_JPEG_BYTES = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];

        /// <summary>
        /// Official WebP RIFF container header: <c>"RIFF"</c> (<c>52 49 46 46</c>), dummy 4-byte size, <c>"WEBP"</c> at offset 8 (<c>57 45 42 50</c>), and <c>'V'</c>.
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
}
