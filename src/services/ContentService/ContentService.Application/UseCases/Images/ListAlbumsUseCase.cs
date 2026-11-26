using ContentService.Application.Interfaces;
using ContentService.Application.Models;
using ContentService.Domain.ValueObjects;

namespace ContentService.Application.UseCases.Images;

/// <summary>
/// Lists immediate child album paths beneath an optional parent album across active images.
/// </summary>
public sealed class ListAlbumsUseCase
{
        private readonly IImageRepository _imageRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="ListAlbumsUseCase"/> class.
        /// </summary>
        /// <param name="imageRepository">The image metadata repository.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="imageRepository"/> is <see langword="null"/>.</exception>
        public ListAlbumsUseCase(IImageRepository imageRepository)
        {
                ArgumentNullException.ThrowIfNull(argument: imageRepository);
                this._imageRepository = imageRepository;
        }

        /// <summary>
        /// Queries distinct immediate sub-albums directly beneath <see cref="ListAlbumsQuery.ParentAlbumPath"/>.
        /// </summary>
        /// <param name="query">The album hierarchy query criteria.</param>
        /// <param name="cancellationToken">A token to observe while querying albums.</param>
        /// <returns>A sorted read-only list of immediate child album paths.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
        public async Task<IReadOnlyList<string>> ExecuteAsync(
                ListAlbumsQuery query,
                CancellationToken cancellationToken)
        {
                ArgumentNullException.ThrowIfNull(argument: query);

                string? normalizedParent = AlbumPath.NormalizeOptional(rawAlbumPath: query.ParentAlbumPath);
                return await this._imageRepository.ListChildAlbumPathsAsync(
                        parentAlbumPath: normalizedParent,
                        cancellationToken: cancellationToken);
        }
}

