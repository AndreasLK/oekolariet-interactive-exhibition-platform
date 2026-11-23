using ContentService.Application.Common;
using ContentService.Application.Models;
using ContentService.Application.UseCases.Images;
using ContentService.Facade.Dtos;
using ContentService.Facade.Interfaces;
using ContentService.Facade.Mapping;

namespace ContentService.Facade.Services;

/// <summary>
/// Default <see cref="IImageQueryFacade"/> serving read-only image catalog, album, metadata, and stream queries.
/// </summary>
internal sealed class ImageQueryFacade : IImageQueryFacade
{
        /// <summary>
        /// Application use case querying paginated image catalog slices.
        /// </summary>
        private readonly ListImagesUseCase _listImagesUseCase;

        /// <summary>
        /// Application use case querying distinct child album paths.
        /// </summary>
        private readonly ListAlbumsUseCase _listAlbumsUseCase;

        /// <summary>
        /// Application use case retrieving image metadata without binary streaming.
        /// </summary>
        private readonly GetImageMetadataUseCase _getImageMetadataUseCase;

        /// <summary>
        /// Application use case opening raw binary image streams.
        /// </summary>
        private readonly GetImageStreamUseCase _getImageStreamUseCase;

        /// <summary>
        /// Application use case opening or lazily generating lightweight preview streams.
        /// </summary>
        private readonly GetImagePreviewUseCase _getImagePreviewUseCase;

        /// <summary>
        /// Initializes a new instance of <see cref="ImageQueryFacade"/>.
        /// </summary>
        /// <param name="listImagesUseCase">The list images use case.</param>
        /// <param name="listAlbumsUseCase">The list albums use case.</param>
        /// <param name="getImageMetadataUseCase">The get image metadata use case.</param>
        /// <param name="getImageStreamUseCase">The get image stream use case.</param>
        /// <param name="getImagePreviewUseCase">The get image preview use case.</param>
        public ImageQueryFacade(
                ListImagesUseCase listImagesUseCase,
                ListAlbumsUseCase listAlbumsUseCase,
                GetImageMetadataUseCase getImageMetadataUseCase,
                GetImageStreamUseCase getImageStreamUseCase,
                GetImagePreviewUseCase getImagePreviewUseCase)
        {
                ArgumentNullException.ThrowIfNull(listImagesUseCase);
                ArgumentNullException.ThrowIfNull(listAlbumsUseCase);
                ArgumentNullException.ThrowIfNull(getImageMetadataUseCase);
                ArgumentNullException.ThrowIfNull(getImageStreamUseCase);
                ArgumentNullException.ThrowIfNull(getImagePreviewUseCase);

                this._listImagesUseCase = listImagesUseCase;
                this._listAlbumsUseCase = listAlbumsUseCase;
                this._getImageMetadataUseCase = getImageMetadataUseCase;
                this._getImageStreamUseCase = getImageStreamUseCase;
                this._getImagePreviewUseCase = getImagePreviewUseCase;
        }

        /// <inheritdoc />
        public async Task<PagedResultDto<ImageMetadataDto>> ListImagesAsync(
                ListImagesQueryDto query,
                CancellationToken cancellationToken = default)
        {
                ArgumentNullException.ThrowIfNull(query);

                ListImagesQuery appQuery = new()
                {
                        Page = query.Page,
                        PageSize = query.PageSize,
                        SearchTerm = query.SearchTerm,
                        AlbumPath = query.AlbumPath,
                        IncludeSubalbums = query.IncludeSubalbums
                };

                PagedResult<ImageMetadataResult> result = await this._listImagesUseCase.ExecuteAsync(
                        query: appQuery,
                        cancellationToken: cancellationToken);

                return FacadeMapper.ToPagedDto(
                        source: result,
                        mapItem: FacadeMapper.ToDto);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<string>> ListAlbumsAsync(
                string? parentAlbumPath = null,
                CancellationToken cancellationToken = default)
        {
                return this._listAlbumsUseCase.ExecuteAsync(
                        query: new ListAlbumsQuery { ParentAlbumPath = parentAlbumPath },
                        cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public async Task<ImageMetadataDto> GetImageMetadataAsync(
                Guid imageId,
                CancellationToken cancellationToken = default)
        {
                ImageMetadataResult result = await this._getImageMetadataUseCase.ExecuteAsync(
                        imageId: imageId,
                        cancellationToken: cancellationToken);

                return FacadeMapper.ToDto(result: result);
        }

        /// <inheritdoc />
        public async Task<ImageStreamDto> GetImageContentAsync(
                Guid imageId,
                CancellationToken cancellationToken = default)
        {
                ImageStreamResult result = await this._getImageStreamUseCase.ExecuteAsync(
                        query: new GetImageStreamQuery { ImageId = imageId },
                        cancellationToken: cancellationToken);

                // Transfer stream disposal ownership to the caller via ImageStreamDto.
                return FacadeMapper.ToDto(result: result);
        }

        /// <inheritdoc />
        public async Task<ImageStreamDto> GetImagePreviewAsync(
                Guid imageId,
                CancellationToken cancellationToken = default)
        {
                ImageStreamResult result = await this._getImagePreviewUseCase.ExecuteAsync(
                        query: new GetImageStreamQuery { ImageId = imageId },
                        cancellationToken: cancellationToken);

                return FacadeMapper.ToDto(result: result);
        }
}

