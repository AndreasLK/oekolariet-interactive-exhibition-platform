using ContentService.Application.Interfaces;
using ContentService.Application.Common;
using ContentService.Application.Models;
using ContentService.Domain.Entities;
using ContentService.Domain.ValueObjects;

namespace ContentService.Application.UseCases.Images;

/// <summary>
/// Retrieves a bounded, paginated slice of active exhibition image metadata.
/// </summary>
public sealed class ListImagesUseCase
{
        /// <summary>
        /// Image metadata repository.
        /// </summary>
        private readonly IImageRepository _imageRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="ListImagesUseCase"/> class.
        /// </summary>
        /// <param name="imageRepository">The image metadata repository.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="imageRepository"/> is <see langword="null"/>.</exception>
        public ListImagesUseCase(IImageRepository imageRepository)
        {
                ArgumentNullException.ThrowIfNull(imageRepository);
                this._imageRepository = imageRepository;
        }

        /// <summary>
        /// Executes a paginated query for active images after clamping page and page-size bounds.
        /// </summary>
        /// <param name="query">The pagination and optional file-name search criteria.</param>
        /// <param name="cancellationToken">A token to observe while querying images.</param>
        /// <returns>A <see cref="PagedResult{T}"/> of projected <see cref="ImageMetadataResult"/> items.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
        public async Task<PagedResult<ImageMetadataResult>> ExecuteAsync(
                ListImagesQuery query,
                CancellationToken cancellationToken)
        {
                ArgumentNullException.ThrowIfNull(query);

                // Clamp invalid client pagination gracefully instead of throwing so UI gallery tables always recover cleanly.
                int clampedPageSize = Math.Clamp(
                        value: query.PageSize,
                        min: ApplicationConstants.MIN_PAGE_SIZE,
                        max: ApplicationConstants.MAX_PAGE_SIZE);

                // Bound Page so downstream (page - 1) * pageSize skip calculations cannot overflow 32-bit signed int.
                int maxValidPage = (int)Math.Min(val1: int.MaxValue, val2: ((long)int.MaxValue / clampedPageSize) + 1L);
                int clampedPage = Math.Clamp(
                        value: query.Page,
                        min: ApplicationConstants.DEFAULT_PAGE,
                        max: maxValidPage);

                string? normalizedAlbumPath = AlbumPath.NormalizeOptional(rawAlbumPath: query.AlbumPath);

                PagedResult<ImageEntity> pagedEntities = await this._imageRepository.ListAsync(
                        page: clampedPage,
                        pageSize: clampedPageSize,
                        searchTerm: query.SearchTerm,
                        albumPath: normalizedAlbumPath,
                        includeSubalbums: query.IncludeSubalbums,
                        cancellationToken: cancellationToken);

                IReadOnlyList<ImageMetadataResult> projectedItems = pagedEntities.Items
                        .Select(ContentResultMapper.ToMetadataResult)
                        .ToArray();

                return new PagedResult<ImageMetadataResult>
                {
                        Items = projectedItems,
                        Page = pagedEntities.Page,
                        PageSize = pagedEntities.PageSize,
                        TotalCount = pagedEntities.TotalCount
                };
        }
}

