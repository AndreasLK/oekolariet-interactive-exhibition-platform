using System.Security.Cryptography;
using ContentService.Domain.Common;
using ContentService.Domain.Entities;

namespace ContentService.Tests.Infrastructure;

/// <summary>
/// Shared constants and image fixture builders for SQL Server infrastructure integration tests.
/// </summary>
internal static class InfrastructureTestFixtures
{
        public const string VALID_NODE_ID = "node-1";
        public const string VALID_ACTOR_ID = "infra-tester";

        /// <summary>
        /// Official 8-byte PNG magic header used for binary persistence fixtures.
        /// </summary>
        public static readonly byte[] PNG_HEADER_BYTES = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        public static ImageEntity CreateSeededImage(byte[] payloadBytes)
        {
                string hash = Convert.ToHexStringLower(SHA256.HashData(payloadBytes));
                return ImageEntity.Create(
                        storageNodeId: VALID_NODE_ID,
                        fileName: "infra-test.png",
                        contentType: DomainConstants.CONTENT_TYPE_PNG,
                        fileSizeBytes: payloadBytes.Length,
                        sha256Hash: hash,
                        actorId: VALID_ACTOR_ID);
        }
}

