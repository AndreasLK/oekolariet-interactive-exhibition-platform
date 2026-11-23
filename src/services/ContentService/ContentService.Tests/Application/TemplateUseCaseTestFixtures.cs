using System.Security.Cryptography;
using ContentService.Application.Options;
using ContentService.Application.UseCases.Templates;
using ContentService.Domain.Common;
using ContentService.Domain.Entities;
using ContentService.Infrastructure.Repositories;
using MicrosoftOptions = Microsoft.Extensions.Options.Options;

namespace ContentService.Tests.Application;

/// <summary>
/// Shared constants and entity/use-case factory helpers for SQL Server template use case integration tests.
/// </summary>
internal static class TemplateUseCaseTestFixtures
{
        public const string VALID_NODE_ID = "node-1";
        public const string OTHER_NODE_ID = "node-2";
        public const string VALID_ACTOR_ID = "editor-1";
        public const string EMPTY_JSON_OBJECT = "{}";

        /// <summary>
        /// Official 8-byte PNG magic header used when seeding referenced test images.
        /// </summary>
        public static readonly byte[] SAMPLE_IMAGE_BYTES = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        public static CreateTemplateUseCase BuildCreateTemplateUseCase(
                SqlServerTemplateRepository templateRepository,
                SqlServerImageRepository imageRepository)
        {
                ContentStorageOptions options = new() { StorageNodeId = VALID_NODE_ID };
                return new CreateTemplateUseCase(
                        templateRepository: templateRepository,
                        imageRepository: imageRepository,
                        options: MicrosoftOptions.Create(options));
        }

        public static ImageEntity CreateSeededImage(string storageNodeId)
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

