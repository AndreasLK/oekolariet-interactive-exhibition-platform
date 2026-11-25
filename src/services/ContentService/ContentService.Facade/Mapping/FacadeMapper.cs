using ContentService.Application.Common;
using ContentService.Application.Models;
using ContentService.Domain.Enums;
using ContentService.Facade.Dtos;

namespace ContentService.Facade.Mapping;

/// <summary>
/// Maps between Application use-case models and Facade DTOs so the API transport layer stays decoupled from Application and Domain types.
/// </summary>
internal static class FacadeMapper
{
        /// <summary>
        /// Projects an Application <see cref="ImageMetadataResult"/> to a Facade <see cref="ImageMetadataDto"/>.
        /// </summary>
        /// <param name="result">The application image metadata result.</param>
        /// <returns>The mapped <see cref="ImageMetadataDto"/>.</returns>
        public static ImageMetadataDto ToDto(ImageMetadataResult result)
        {
                return new ImageMetadataDto
                {
                        Id = result.Id,
                        PictureReference = result.PictureReference,
                        ExternalId = result.ExternalId,
                        FileName = result.FileName,
                        ContentType = result.ContentType,
                        SizeBytes = result.SizeBytes,
                        Sha256Hash = result.Sha256Hash,
                        AltText = result.AltText,
                        AlbumPath = result.AlbumPath,
                        Tags = result.Tags,
                        CreatedAt = result.CreatedAt,
                        ConcurrencyStamp = result.ConcurrencyStamp
                };
        }

        /// <summary>
        /// Projects an Application <see cref="ImageStreamResult"/> to a disposable Facade <see cref="ImageStreamDto"/>.
        /// </summary>
        /// <param name="result">The application image stream result.</param>
        /// <returns>A disposable <see cref="ImageStreamDto"/> owning the underlying stream lifetime.</returns>
        public static ImageStreamDto ToDto(ImageStreamResult result)
        {
                return new ImageStreamDto(
                        metadata: ToDto(result: result.Metadata),
                        content: result.Content,
                        innerDisposable: result);
        }

        /// <summary>
        /// Projects an Application <see cref="TemplateResult"/> to a Facade <see cref="TemplateMetadataDto"/>.
        /// </summary>
        /// <param name="result">The application template result.</param>
        /// <returns>The mapped <see cref="TemplateMetadataDto"/>.</returns>
        public static TemplateMetadataDto ToDto(TemplateResult result)
        {
                return new TemplateMetadataDto
                {
                        Id = result.Id,
                        ExternalId = result.ExternalId,
                        Name = result.Name,
                        ComponentType = ToDto(componentType: result.ComponentType),
                        SchemaJson = result.SchemaJson,
                        SchemaVersion = result.SchemaVersion,
                        IsArchived = result.IsArchived,
                        ReferencedPictureReferences = result.ReferencedPictureReferences,
                        CreatedAt = result.CreatedAt,
                        UpdatedAt = result.UpdatedAt,
                        ConcurrencyStamp = result.ConcurrencyStamp
                };
        }

        /// <summary>
        /// Projects a paginated Application result to a paginated Facade DTO using the provided item mapper.
        /// </summary>
        /// <typeparam name="TSource">The application item type.</typeparam>
        /// <typeparam name="TDestination">The facade DTO item type.</typeparam>
        /// <param name="source">The application paged result.</param>
        /// <param name="mapItem">The item projection delegate.</param>
        /// <returns>A <see cref="PagedResultDto{T}"/> containing mapped DTO items.</returns>
        public static PagedResultDto<TDestination> ToPagedDto<TSource, TDestination>(
                PagedResult<TSource> source,
                Func<TSource, TDestination> mapItem)
        {
                IReadOnlyList<TDestination> items = source.Items
                        .Select(selector: mapItem)
                        .ToArray();

                return new PagedResultDto<TDestination>
                {
                        Items = items,
                        Page = source.Page,
                        PageSize = source.PageSize,
                        TotalCount = source.TotalCount
                };
        }

        /// <summary>
        /// Projects an Application <see cref="PublishBundleResult"/> to a Facade <see cref="PublishBundleDto"/>.
        /// </summary>
        /// <param name="result">The application publish bundle result.</param>
        /// <returns>The mapped <see cref="PublishBundleDto"/>.</returns>
        public static PublishBundleDto ToDto(PublishBundleResult result)
        {
                IReadOnlyList<BundleAssetDto> assets = result.Assets
                        .Select(asset => new BundleAssetDto
                        {
                                ImageId = asset.ImageId,
                                PictureReference = asset.PictureReference,
                                ExternalId = asset.ExternalId,
                                ContentType = asset.ContentType,
                                SizeBytes = asset.SizeBytes,
                                Sha256Hash = asset.Sha256Hash
                        })
                        .ToArray();

                return new PublishBundleDto
                {
                        TemplateId = result.TemplateId,
                        TemplateSchemaJson = result.TemplateSchemaJson,
                        ImplementationJson = result.ImplementationJson,
                        Assets = assets,
                        BundleChecksum = result.BundleChecksum
                };
        }

        /// <summary>
        /// Projects an Application <see cref="StorageHealthResult"/> to a Facade <see cref="StorageHealthDto"/>.
        /// </summary>
        /// <param name="result">The application storage health result.</param>
        /// <returns>The mapped <see cref="StorageHealthDto"/>.</returns>
        public static StorageHealthDto ToDto(StorageHealthResult result)
        {
                return new StorageHealthDto
                {
                        Status = (StorageHealthStatusDto)(int)result.Status,
                        UsedBytes = result.UsedBytes,
                        QuotaBytes = result.QuotaBytes,
                        UtilizationPercent = result.UtilizationPercent
                };
        }

        /// <summary>
        /// Maps a Domain <see cref="TemplateComponentType"/> enum value to <see cref="TemplateComponentTypeDto"/>.
        /// </summary>
        /// <param name="componentType">The domain component type.</param>
        /// <returns>The corresponding <see cref="TemplateComponentTypeDto"/>.</returns>
        public static TemplateComponentTypeDto ToDto(TemplateComponentType componentType) =>
                (TemplateComponentTypeDto)(int)componentType;

        /// <summary>
        /// Maps a Facade <see cref="TemplateComponentTypeDto"/> enum value to Domain <see cref="TemplateComponentType"/>.
        /// </summary>
        /// <param name="componentType">The facade component type DTO.</param>
        /// <returns>The corresponding <see cref="TemplateComponentType"/>.</returns>
        public static TemplateComponentType ToDomain(TemplateComponentTypeDto componentType) =>
                (TemplateComponentType)(int)componentType;

        /// <summary>
        /// Projects an Application <see cref="PoolNodeOffer"/> to a Facade <see cref="PoolNodeOfferDto"/>.
        /// </summary>
        /// <param name="offer">The application pool node offer.</param>
        /// <returns>The mapped <see cref="PoolNodeOfferDto"/>.</returns>
        public static PoolNodeOfferDto ToDto(PoolNodeOffer offer)
        {
                return new PoolNodeOfferDto
                {
                        StorageNodeId = offer.StorageNodeId,
                        DaprAppId = offer.DaprAppId,
                        PublicBaseUrl = offer.PublicBaseUrl,
                        AvailableBytes = offer.AvailableBytes
                };
        }

        /// <summary>
        /// Maps a Facade <see cref="PoolNodeOfferDto"/> back to an Application <see cref="PoolNodeOffer"/>.
        /// </summary>
        /// <param name="offer">The facade pool node offer DTO.</param>
        /// <returns>The mapped <see cref="PoolNodeOffer"/>.</returns>
        public static PoolNodeOffer ToApplication(PoolNodeOfferDto offer)
        {
                return new PoolNodeOffer
                {
                        StorageNodeId = offer.StorageNodeId,
                        DaprAppId = offer.DaprAppId,
                        PublicBaseUrl = offer.PublicBaseUrl,
                        AvailableBytes = offer.AvailableBytes
                };
        }
}

