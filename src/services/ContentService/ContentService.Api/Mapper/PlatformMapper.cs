using ContentService.Facade.Dtos;
using ContentService.Sdk.Models;

namespace ContentService.Api.Mapper;

/// <summary>
/// Translates between <see cref="ContentService.Sdk.Models"/> platform contracts (health, publishing, and pool) and <see cref="ContentService.Facade.Dtos"/>.
/// </summary>
internal static class PlatformMapper
{
        /// <summary>
        /// Translates a Facade <see cref="StorageHealthDto"/> into an SDK <see cref="ContentStorageHealthResponse"/>.
        /// </summary>
        /// <param name="dto">The storage health DTO returned by <c>IContentPlatformQueryFacade</c>.</param>
        /// <returns>The public SDK <see cref="ContentStorageHealthResponse"/>.</returns>
        public static ContentStorageHealthResponse ToResponse(StorageHealthDto dto)
        {
                // Map the facade health status enum and quota metrics to the SDK response.
                return new ContentStorageHealthResponse
                {
                        Status = ToSdkHealthStatus(status: dto.Status),
                        UsedBytes = dto.UsedBytes,
                        QuotaBytes = dto.QuotaBytes,
                        UtilizationPercent = dto.UtilizationPercent
                };
        }

        /// <summary>
        /// Translates an SDK <see cref="ContentResolvePublishBundleQuery"/> into a Facade <see cref="ResolvePublishBundleQueryDto"/>.
        /// </summary>
        /// <param name="query">The incoming SDK publish bundle resolution query.</param>
        /// <returns>The query DTO ready for <c>IContentPlatformQueryFacade</c>.</returns>
        public static ResolvePublishBundleQueryDto ToQuery(ContentResolvePublishBundleQuery query)
        {
                return new ResolvePublishBundleQueryDto
                {
                        TemplateId = query.TemplateId,
                        ImplementationJson = query.ImplementationJson
                };
        }

        /// <summary>
        /// Translates a Facade <see cref="PublishBundleDto"/> into an SDK <see cref="ContentPublishBundleResponse"/>.
        /// </summary>
        /// <param name="dto">The resolved publish bundle DTO returned by <c>IContentPlatformQueryFacade</c>.</param>
        /// <returns>The public SDK <see cref="ContentPublishBundleResponse"/>.</returns>
        public static ContentPublishBundleResponse ToResponse(PublishBundleDto dto)
        {
                // Project each verified bundle asset DTO into its SDK response counterpart.
                List<ContentBundleAssetResponse> assets = new(capacity: dto.Assets.Count);
                foreach (BundleAssetDto asset in dto.Assets)
                {
                        assets.Add(item: ToResponse(dto: asset));
                }

                return new ContentPublishBundleResponse
                {
                        TemplateId = dto.TemplateId,
                        TemplateSchemaJson = dto.TemplateSchemaJson,
                        ImplementationJson = dto.ImplementationJson,
                        Assets = assets,
                        BundleChecksum = dto.BundleChecksum
                };
        }

        /// <summary>
        /// Translates a Facade <see cref="PoolNodeOfferDto"/> into an SDK <see cref="ContentPoolNodeOfferResponse"/>.
        /// </summary>
        /// <param name="dto">The winning storage node offer DTO returned by <c>IPoolFacade</c>.</param>
        /// <returns>The public SDK <see cref="ContentPoolNodeOfferResponse"/>.</returns>
        public static ContentPoolNodeOfferResponse ToResponse(PoolNodeOfferDto dto)
        {
                return new ContentPoolNodeOfferResponse
                {
                        StorageNodeId = dto.StorageNodeId,
                        DaprAppId = dto.DaprAppId,
                        PublicBaseUrl = dto.PublicBaseUrl,
                        AvailableBytes = dto.AvailableBytes
                };
        }

        /// <summary>
        /// Translates a Facade <see cref="BundleAssetDto"/> into an SDK <see cref="ContentBundleAssetResponse"/>.
        /// </summary>
        private static ContentBundleAssetResponse ToResponse(BundleAssetDto dto)
        {
                return new ContentBundleAssetResponse
                {
                        ImageId = dto.ImageId,
                        PictureReference = dto.PictureReference,
                        ExternalId = dto.ExternalId,
                        ContentType = dto.ContentType,
                        SizeBytes = dto.SizeBytes,
                        Sha256Hash = dto.Sha256Hash
                };
        }

        /// <summary>
        /// Maps a Facade <see cref="StorageHealthStatusDto"/> value to its SDK <see cref="ContentStorageHealthStatus"/> counterpart.
        /// </summary>
        private static ContentStorageHealthStatus ToSdkHealthStatus(StorageHealthStatusDto status)
        {
                return status switch
                {
                        StorageHealthStatusDto.Healthy => ContentStorageHealthStatus.Healthy,
                        StorageHealthStatusDto.Warning => ContentStorageHealthStatus.Warning,
                        StorageHealthStatusDto.FullReadOnly => ContentStorageHealthStatus.FullReadOnly,
                        _ => throw new ArgumentOutOfRangeException(
                                paramName: nameof(status),
                                actualValue: status,
                                message: "Unsupported StorageHealthStatusDto value.")
                };
        }
}

