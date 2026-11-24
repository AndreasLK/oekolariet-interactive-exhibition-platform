using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using ContentService.Application.Interfaces;
using ContentService.Application.Common;
using ContentService.Application.Models;
using ContentService.Domain.Common;
using ContentService.Domain.Entities;
using ContentService.Domain.Exceptions;
using ContentService.Domain.ValueObjects;

namespace ContentService.Application.UseCases.Publishing;

/// <summary>
/// Validates and seals an exhibition implementation and optional template into a deterministic offline kiosk bundle so kiosks can verify asset completeness and integrity with a single checksum.
/// </summary>
public sealed class ResolvePublishBundleUseCase
{
        /// <summary>
        /// Loads optional templates (including archived ones) when an exhibition references a saved layout schema.
        /// </summary>
        private readonly ITemplateRepository _templateRepository;

        /// <summary>
        /// Resolves active image metadata and SHA-256 digests so offline kiosks receive verifiable download manifests.
        /// </summary>
        private readonly IImageRepository _imageRepository;

        /// <summary>
        /// Captures template and image repositories so bundle validation and deterministic hashing stay independent of SQL Server.
        /// </summary>
        /// <param name="templateRepository">The template persistence repository.</param>
        /// <param name="imageRepository">The image metadata repository.</param>
        /// <exception cref="ArgumentNullException">Thrown when any required dependency is <see langword="null"/>.</exception>
        public ResolvePublishBundleUseCase(
                ITemplateRepository templateRepository,
                IImageRepository imageRepository)
        {
                ArgumentNullException.ThrowIfNull(templateRepository);
                ArgumentNullException.ThrowIfNull(imageRepository);

                this._templateRepository = templateRepository;
                this._imageRepository = imageRepository;
        }

        /// <summary>
        /// Resolves all referenced image assets across template and implementation JSON and seals them with a canonical SHA-256 bundle checksum for offline kiosk sync.
        /// </summary>
        /// <param name="command">The publish bundle resolution request.</param>
        /// <param name="cancellationToken">Propagates cancellation while loading templates and resolving image metadata.</param>
        /// <returns>A deterministic <see cref="PublishBundleResult"/> ready for offline kiosk packaging.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="command"/> is <see langword="null"/>.</exception>
        /// <exception cref="DomainValidationException">Thrown when implementation JSON is invalid/oversized, contains base64 data URIs, or <see cref="ResolvePublishBundleCommand.TemplateId"/> is <see cref="Guid.Empty"/>.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the specified template or any referenced picture reference does not exist.</exception>
        public async Task<PublishBundleResult> ExecuteAsync(
                ResolvePublishBundleCommand command,
                CancellationToken cancellationToken)
        {
                ArgumentNullException.ThrowIfNull(command);

                ReferencedImageVerifier.ValidateJsonObject(
                        jsonPayload: command.ImplementationJson,
                        maxLength: ApplicationConstants.MAX_IMPLEMENTATION_JSON_LENGTH,
                        fieldName: ApplicationConstants.IMPLEMENTATION_JSON_FIELD_NAME);

                if (command.TemplateId.HasValue && command.TemplateId.Value == Guid.Empty)
                {
                        throw new DomainValidationException("Template ID cannot be empty when specified.");
                }

                // Extract implementation picture references before hitting the database so embedded base64 data URIs fail fast without DB round-trips.
                HashSet<PictureReference> combinedReferences =
                        [.. PictureReference.ExtractReferences(jsonPayload: command.ImplementationJson)];

                TemplateEntity? template = await this.LoadTemplateIfRequestedAsync(
                        templateId: command.TemplateId,
                        cancellationToken: cancellationToken);
                if (template is not null)
                {
                        foreach (PictureReference templateReference in PictureReference.ExtractReferences(jsonPayload: template.SchemaJson))
                        {
                                combinedReferences.Add(templateReference);
                        }
                }

                IReadOnlyList<ImageEntity> resolvedImages = await ReferencedImageVerifier.VerifyAndResolveAsync(
                        imageRepository: this._imageRepository,
                        references: combinedReferences,
                        cancellationToken: cancellationToken);

                // Sort canonically by formatted GUID then StorageNodeId so identical exhibition content yields the exact same manifest and checksum regardless of JSON order.
                List<ImageEntity> orderedImages =
                [
                        .. resolvedImages
                                .OrderBy(
                                        keySelector: image => image.Id.ToString(DomainConstants.GUID_FORMAT_HYPHENATED),
                                        comparer: StringComparer.Ordinal)
                                .ThenBy(
                                        keySelector: image => image.StorageNodeId,
                                        comparer: StringComparer.Ordinal)
                ];

                IReadOnlyList<BundleAssetResult> assets = orderedImages
                        .Select(ContentResultMapper.ToBundleAssetResult)
                        .ToArray();

                string? templateSchemaJson = template?.SchemaJson;
                string bundleChecksum = ComputeBundleChecksum(
                        templateSchemaJson: templateSchemaJson,
                        implementationJson: command.ImplementationJson,
                        orderedImages: orderedImages);

                return new PublishBundleResult
                {
                        TemplateId = command.TemplateId,
                        TemplateSchemaJson = templateSchemaJson,
                        ImplementationJson = command.ImplementationJson,
                        Assets = assets,
                        BundleChecksum = bundleChecksum
                };
        }

        /// <summary>
        /// Loads the linked template when specified, intentionally permitting archived templates so in-flight exhibition drafts do not break when a template is retired.
        /// </summary>
        /// <param name="templateId">The optional template identifier.</param>
        /// <param name="cancellationToken">Propagates cancellation while querying the template repository.</param>
        /// <returns>The loaded <see cref="TemplateEntity"/>, or <see langword="null"/> when publishing a standalone implementation without a template.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when a non-null <paramref name="templateId"/> does not exist.</exception>
        private async Task<TemplateEntity?> LoadTemplateIfRequestedAsync(
                Guid? templateId,
                CancellationToken cancellationToken)
        {
                // Templates are optional for ad-hoc implementations; when referenced, archived templates remain readable so existing drafts can still publish.
                if (!templateId.HasValue)
                {
                        return null;
                }

                TemplateEntity? template = await this._templateRepository.GetByIdAsync(
                        id: templateId.Value,
                        cancellationToken: cancellationToken);
                if (template is null)
                {
                        throw new KeyNotFoundException($"Template '{templateId.Value}' was not found.");
                }

                return template;
        }

        /// <summary>
        /// Computes a canonical SHA-256 checksum over length-prefixed segments so kiosks detect any tamper or mismatch without ambiguity from arbitrary JSON characters.
        /// </summary>
        /// <param name="templateSchemaJson">The optional template schema JSON.</param>
        /// <param name="implementationJson">The exhibition implementation JSON.</param>
        /// <param name="orderedImages">The deterministically ordered list of verified image assets.</param>
        /// <returns>The lowercase 64-character hexadecimal SHA-256 bundle checksum.</returns>
        private static string ComputeBundleChecksum(
                string? templateSchemaJson,
                string implementationJson,
                IReadOnlyList<ImageEntity> orderedImages)
        {
                using IncrementalHash sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

                // Length-prefix every segment instead of joining with delimiters so arbitrary JSON contents can never cause framing collisions.
                AppendLengthPrefixedUtf8(sha256: sha256, value: templateSchemaJson ?? string.Empty);
                AppendLengthPrefixedUtf8(sha256: sha256, value: implementationJson);

                foreach (ImageEntity image in orderedImages)
                {
                        string assetRecord =
                                $"{image.StorageNodeId}{ApplicationConstants.CHECKSUM_FIELD_SEPARATOR}{image.Id.ToString(DomainConstants.GUID_FORMAT_HYPHENATED)}{ApplicationConstants.CHECKSUM_FIELD_SEPARATOR}{image.Sha256Hash}";
                        AppendLengthPrefixedUtf8(sha256: sha256, value: assetRecord);
                }

                return Convert.ToHexStringLower(sha256.GetHashAndReset());
        }

        /// <summary>
        /// Writes a 4-byte length header before each UTF-8 segment so adjacent fields cannot merge ambiguously.
        /// </summary>
        /// <param name="sha256">The incremental SHA-256 hash accumulator.</param>
        /// <param name="value">The string segment to encode and append.</param>
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
