using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using ContentService.Application.Common;
using ContentService.Application.Models;
using ContentService.Domain.Common;
using ContentService.Domain.ValueObjects;

namespace ContentService.Application.UseCases.Publishing;

/// <summary>
/// Computes canonical, length-prefixed SHA-256 checksums for offline kiosk publish bundles.
/// </summary>
internal static class PublishBundleChecksumCalculator
{
        /// <summary>
        /// Computes a canonical SHA-256 checksum over length-prefixed segments so kiosks detect any tamper or mismatch without ambiguity from arbitrary JSON characters.
        /// </summary>
        /// <param name="templateSchemaJson">The optional template schema JSON.</param>
        /// <param name="implementationJson">The exhibition implementation JSON.</param>
        /// <param name="orderedImages">The deterministically ordered list of verified image assets.</param>
        /// <returns>The lowercase 64-character hexadecimal SHA-256 bundle checksum.</returns>
        public static string Compute(
                string? templateSchemaJson,
                string implementationJson,
                IReadOnlyList<ImageMetadataResult> orderedImages)
        {
                using IncrementalHash sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

                // Length-prefix every segment instead of joining with delimiters so arbitrary JSON contents can never cause framing collisions.
                AppendLengthPrefixedUtf8(sha256: sha256, value: templateSchemaJson ?? string.Empty);
                AppendLengthPrefixedUtf8(sha256: sha256, value: implementationJson);

                foreach (ImageMetadataResult image in orderedImages)
                {
                        string storageNodeId = PictureReference.ParseExternalId(image.ExternalId).StorageNodeId;
                        string assetRecord =
                                $"{storageNodeId}{ApplicationConstants.CHECKSUM_FIELD_SEPARATOR}{image.Id.ToString(DomainConstants.GUID_FORMAT_HYPHENATED)}{ApplicationConstants.CHECKSUM_FIELD_SEPARATOR}{image.Sha256Hash}";
                        AppendLengthPrefixedUtf8(sha256: sha256, value: assetRecord);
                }

                return Convert.ToHexStringLower(sha256.GetHashAndReset());
        }

        /// <summary>
        /// Writes a 4-byte length header before each UTF-8 segment so adjacent fields cannot merge ambiguously.
        /// </summary>
        private static void AppendLengthPrefixedUtf8(IncrementalHash sha256, string value)
        {
                // Without length prefixes, ("ab", "c") and ("a", "bc") would feed the identical byte sequence "abc" into SHA-256.
                // Writing each segment's exact UTF-8 byte length first in fixed big-endian byte order keeps boundaries unambiguous on every CPU.
                byte[] utf8Bytes = Encoding.UTF8.GetBytes(value);
                byte[] lengthPrefix = new byte[sizeof(int)];
                BinaryPrimitives.WriteInt32BigEndian(destination: lengthPrefix, value: utf8Bytes.Length);
                sha256.AppendData(lengthPrefix);
                sha256.AppendData(utf8Bytes);
        }
}

