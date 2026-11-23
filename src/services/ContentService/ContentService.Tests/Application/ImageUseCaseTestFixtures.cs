using System.Security.Cryptography;
using ContentService.Application.Options;
using ContentService.Application.UseCases.Images;
using ContentService.Application.Validation;
using ContentService.Domain.Common;
using ContentService.Domain.Entities;
using ContentService.Infrastructure.Repositories;
using MicrosoftOptions = Microsoft.Extensions.Options.Options;

namespace ContentService.Tests.Application;

/// <summary>
/// Shared fixtures and factory helpers for SQL Server image use case integration tests.
/// </summary>
internal static class ImageUseCaseTestFixtures
{
        public const string VALID_NODE_ID = "node-1";
        public const string VALID_ACTOR_ID = "curator-1";
        public const string VALID_FILE_NAME = "exhibit.png";
        public const long DEFAULT_TEST_QUOTA_BYTES = 10_000L;

        /// <summary>
        /// Minimal valid PNG payload: 8-byte PNG magic header plus 4 dummy body bytes.
        /// </summary>
        public static readonly byte[] VALID_PNG_PAYLOAD = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02, 0x03, 0x04];

        public static UploadImageUseCase CreateUploadUseCase(
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

        public static ImageEntity CreateSeededImage(byte[] payloadBytes)
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
}

