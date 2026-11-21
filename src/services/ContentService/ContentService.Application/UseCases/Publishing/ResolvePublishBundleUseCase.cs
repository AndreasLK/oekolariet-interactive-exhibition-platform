using System.Buffers;
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
/// Resolves all picture dependencies across an optional template and implementation JSON into a deterministic, checksum-sealed offline kiosk bundle.
/// </summary>
public sealed class ResolvePublishBundleUseCase
{
        /// <summary>
        /// Template aggregate repository.
        /// </summary>
        private readonly ITemplateRepository _templateRepository;

        /// <summary>
        /// Image metadata repository used to verify and resolve bundle assets.
        /// </summary>
        private readonly IImageRepository _imageRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="ResolvePublishBundleUseCase"/> class.
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
        /// Validates implementation JSON, loads any linked template, verifies all referenced image assets exist, and computes a deterministic bundle checksum.
        /// </summary>
        /// <param name="command">The publish bundle resolution request.</param>
        /// <param name="cancellationToken">A token to observe while loading templates and image metadata.</param>
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

                // Extract implementation references before hitting the database so embedded base64 data URIs fail immediately.
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

                // Sort deterministically by formatted GUID string then StorageNodeId so bundle ordering and checksums stay identical regardless of JSON key order.
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
        /// Loads the requested template when <paramref name="templateId"/> is specified, allowing archived templates for active draft publishing.
        /// </summary>
        /// <param name="templateId">The optional template identifier.</param>
        /// <param name="cancellationToken">A token to observe while querying the template repository.</param>
        /// <returns>The loaded <see cref="TemplateEntity"/>, or <see langword="null"/> if <paramref name="templateId"/> is <see langword="null"/>.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when a non-null <paramref name="templateId"/> does not exist.</exception>
        private async Task<TemplateEntity?> LoadTemplateIfRequestedAsync(
                Guid? templateId,
                CancellationToken cancellationToken)
        {
                // Templates are optional for ad-hoc implementations; when specified, archived templates remain readable for existing drafts.
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
        /// Computes a canonical, length-prefixed SHA-256 checksum across template schema, implementation JSON, and sorted asset digests.
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

                // Length-prefix every field instead of joining with delimiters so arbitrary JSON contents can never cause framing collisions.
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
        /// Encodes <paramref name="value"/> as UTF-8 prefixed with its big-endian 32-bit byte length into <paramref name="sha256"/>.
        /// </summary>
        /// <param name="sha256">The incremental SHA-256 hash accumulator.</param>
        /// <param name="value">The string segment to encode and append.</param>
        private static void AppendLengthPrefixedUtf8(IncrementalHash sha256, string value)
        {
                // Rent a pooled buffer sized for worst-case UTF-8 expansion to avoid per-segment heap allocations.
                int maxByteCount = Encoding.UTF8.GetMaxByteCount(value.Length);
                byte[] buffer = ArrayPool<byte>.Shared.Rent(maxByteCount);

                try
                {
                        // Write the actual encoded byte count as a 4-byte big-endian prefix so field boundaries remain unambiguous.
                        int actualByteCount = Encoding.UTF8.GetBytes(chars: value.AsSpan(), bytes: buffer.AsSpan());
                        Span<byte> lengthPrefix = stackalloc byte[sizeof(int)];
                        BinaryPrimitives.WriteInt32BigEndian(destination: lengthPrefix, value: actualByteCount);
                        sha256.AppendData(lengthPrefix);
                        sha256.AppendData(buffer.AsSpan(start: 0, length: actualByteCount));
                }
                finally
                {
                        // Always return the pooled buffer even if encoding or hashing throws.
                        ArrayPool<byte>.Shared.Return(buffer);
                }
        }
}

