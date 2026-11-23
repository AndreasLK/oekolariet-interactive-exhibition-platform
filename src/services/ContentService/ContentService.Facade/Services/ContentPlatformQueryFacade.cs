using ContentService.Application.Models;
using ContentService.Application.UseCases.Health;
using ContentService.Application.UseCases.Publishing;
using ContentService.Facade.Dtos;
using ContentService.Facade.Interfaces;
using ContentService.Facade.Mapping;

namespace ContentService.Facade.Services;

/// <summary>
/// Default <see cref="IContentPlatformQueryFacade"/> serving read-only publish bundle resolution and storage health telemetry.
/// </summary>
internal sealed class ContentPlatformQueryFacade : IContentPlatformQueryFacade
{
        /// <summary>
        /// Application use case resolving and checksum-sealing offline kiosk publish bundles.
        /// </summary>
        private readonly ResolvePublishBundleUseCase _resolvePublishBundleUseCase;

        /// <summary>
        /// Application use case evaluating storage node quota utilization and health.
        /// </summary>
        private readonly GetStorageHealthUseCase _getStorageHealthUseCase;

        /// <summary>
        /// Initializes a new instance of <see cref="ContentPlatformQueryFacade"/>.
        /// </summary>
        /// <param name="resolvePublishBundleUseCase">The resolve publish bundle use case.</param>
        /// <param name="getStorageHealthUseCase">The get storage health use case.</param>
        public ContentPlatformQueryFacade(
                ResolvePublishBundleUseCase resolvePublishBundleUseCase,
                GetStorageHealthUseCase getStorageHealthUseCase)
        {
                ArgumentNullException.ThrowIfNull(resolvePublishBundleUseCase);
                ArgumentNullException.ThrowIfNull(getStorageHealthUseCase);

                this._resolvePublishBundleUseCase = resolvePublishBundleUseCase;
                this._getStorageHealthUseCase = getStorageHealthUseCase;
        }

        /// <inheritdoc />
        public async Task<PublishBundleDto> ResolvePublishBundleAsync(
                ResolvePublishBundleQueryDto query,
                CancellationToken cancellationToken = default)
        {
                ArgumentNullException.ThrowIfNull(query);

                ResolvePublishBundleCommand appCommand = new()
                {
                        TemplateId = query.TemplateId,
                        ImplementationJson = query.ImplementationJson
                };

                PublishBundleResult result = await this._resolvePublishBundleUseCase.ExecuteAsync(
                        command: appCommand,
                        cancellationToken: cancellationToken);

                return FacadeMapper.ToDto(result: result);
        }

        /// <inheritdoc />
        public async Task<StorageHealthDto> GetStorageHealthAsync(CancellationToken cancellationToken = default)
        {
                StorageHealthResult result = await this._getStorageHealthUseCase.ExecuteAsync(
                        cancellationToken: cancellationToken);

                return FacadeMapper.ToDto(result: result);
        }
}

