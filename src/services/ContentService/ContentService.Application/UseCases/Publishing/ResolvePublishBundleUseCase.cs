using ContentService.Application.Common;
using ContentService.Application.Interfaces;
using ContentService.Application.Models;
using ContentService.Application.Options;
using ContentService.Domain.Common;
using ContentService.Domain.Entities;
using ContentService.Domain.Exceptions;
using ContentService.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace ContentService.Application.UseCases.Publishing;

/// <summary>
/// Validates and seals an exhibition implementation and optional template into a deterministic offline kiosk bundle so kiosks can verify asset completeness and integrity with a single checksum.
/// </summary>
public sealed class ResolvePublishBundleUseCase
{
        private readonly ITemplateRepository _templateRepository;
        private readonly IImageRepository _imageRepository;
        private readonly IStorageNodeClient? _storageNodeClient;
        private readonly string? _localStorageNodeId;

        /// <summary>
        /// Captures template and image repositories so bundle validation and deterministic hashing stay independent of SQL Server.
        /// </summary>
        /// <param name="templateRepository">The template persistence repository.</param>
        /// <param name="imageRepository">The image metadata repository.</param>
        /// <param name="storageNodeClient">Optional peer storage node client for cross-node image resolution.</param>
        /// <param name="options">Optional content storage options supplying the local storage node identifier.</param>
        public ResolvePublishBundleUseCase(
                ITemplateRepository templateRepository,
                IImageRepository imageRepository,
                IStorageNodeClient? storageNodeClient = null,
                IOptions<ContentStorageOptions>? options = null)
        {
                ArgumentNullException.ThrowIfNull(templateRepository);
                ArgumentNullException.ThrowIfNull(imageRepository);

                this._templateRepository = templateRepository;
                this._imageRepository = imageRepository;
                this._storageNodeClient = storageNodeClient;
                this._localStorageNodeId = options?.Value.StorageNodeId;
        }

        /// <summary>
        /// Resolves all referenced image assets across template and implementation JSON and seals them with a canonical SHA-256 bundle checksum for offline kiosk sync.
        /// </summary>
        /// <param name="command">The publish bundle resolution request.</param>
        /// <param name="cancellationToken">Propagates cancellation while loading templates and resolving image metadata.</param>
        /// <returns>A deterministic <see cref="PublishBundleResult"/> ready for offline kiosk packaging.</returns>
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

                IReadOnlyList<ImageMetadataResult> resolvedImages = await ReferencedImageVerifier.VerifyAndResolveAsync(
                        imageRepository: this._imageRepository,
                        references: combinedReferences,
                        cancellationToken: cancellationToken,
                        storageNodeClient: this._storageNodeClient,
                        localStorageNodeId: this._localStorageNodeId);

                // Sort canonically by formatted GUID then StorageNodeId so identical exhibition content yields the exact same manifest and checksum regardless of JSON order.
                List<ImageMetadataResult> orderedImages =
                [
                        .. resolvedImages
                                .OrderBy(
                                        keySelector: image => image.Id.ToString(DomainConstants.GUID_FORMAT_HYPHENATED),
                                        comparer: StringComparer.Ordinal)
                                .ThenBy(
                                        keySelector: image => PictureReference.ParseExternalId(image.ExternalId).StorageNodeId,
                                        comparer: StringComparer.Ordinal)
                ];

                IReadOnlyList<BundleAssetResult> assets = orderedImages
                        .Select(ContentResultMapper.ToBundleAssetResult)
                        .ToArray();

                string? templateSchemaJson = template?.SchemaJson;
                string bundleChecksum = PublishBundleChecksumCalculator.Compute(
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
}
