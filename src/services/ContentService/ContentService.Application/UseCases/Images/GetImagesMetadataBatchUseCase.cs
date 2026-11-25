using ContentService.Application.Common;
using ContentService.Application.Interfaces;
using ContentService.Application.Models;
using ContentService.Domain.Entities;

namespace ContentService.Application.UseCases.Images;

/// <summary>
/// Batch-retrieves active image metadata for a collection of image IDs on the local node, eliminating N+1 peer Dapr calls.
/// </summary>
public sealed class GetImagesMetadataBatchUseCase
{
        /// <summary>
        /// Local image metadata repository.
        /// </summary>
        private readonly IImageRepository _imageRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetImagesMetadataBatchUseCase"/> class.
        /// </summary>
        /// <param name="imageRepository">The local image metadata repository.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="imageRepository"/> is <see langword="null"/>.</exception>
        public GetImagesMetadataBatchUseCase(IImageRepository imageRepository)
        {
                ArgumentNullException.ThrowIfNull(imageRepository);
                this._imageRepository = imageRepository;
        }

        /// <summary>
        /// Fetches active metadata projections for all non-deleted matching image IDs on the local node.
        /// </summary>
        /// <param name="imageIds">The image identifiers to look up.</param>
        /// <param name="cancellationToken">Propagates cancellation during repository lookup.</param>
        /// <returns>The active <see cref="ImageMetadataResult"/> projections found locally.</returns>
        public async Task<IReadOnlyList<ImageMetadataResult>> ExecuteAsync(
                IReadOnlyCollection<Guid> imageIds,
                CancellationToken cancellationToken)
        {
                ArgumentNullException.ThrowIfNull(imageIds);

                if (imageIds.Count == 0)
                {
                        return [];
                }

                // Deduplicate incoming IDs and drop empty GUIDs before querying SQL Server.
                HashSet<Guid> distinctIds = [];
                foreach (Guid imageId in imageIds)
                {
                        if (imageId != Guid.Empty)
                        {
                                distinctIds.Add(imageId);
                        }
                }

                if (distinctIds.Count == 0)
                {
                        return [];
                }

                IReadOnlyList<ImageEntity> images = await this._imageRepository.GetByIdsAsync(
                        ids: distinctIds,
                        cancellationToken: cancellationToken);

                List<ImageMetadataResult> results = new(images.Count);
                foreach (ImageEntity image in images)
                {
                        // Exclude soft-deleted images so remote template/bundle verification never binds to deleted assets.
                        if (!image.IsDeleted)
                        {
                                results.Add(ContentResultMapper.ToMetadataResult(image: image));
                        }
                }

                return results;
        }
}

