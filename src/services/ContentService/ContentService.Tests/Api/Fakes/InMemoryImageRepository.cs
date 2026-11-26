using ContentService.Application.Common;
using ContentService.Application.Interfaces;
using ContentService.Domain.Entities;
using ContentService.Domain.Exceptions;

namespace ContentService.Tests.Api.Fakes;

/// <summary>
/// In-memory <see cref="IImageRepository"/> test double for API pipeline tests.
/// </summary>
internal sealed class InMemoryImageRepository : IImageRepository
{
        private readonly InMemoryContentState _state;

        public InMemoryImageRepository(InMemoryContentState state) => this._state = state;

        public Task<ImageEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
                if (this._state.ExceptionToThrowOnGetImage is not null)
                {
                        throw this._state.ExceptionToThrowOnGetImage;
                }

                return Task.FromResult(result: this._state.Images.TryGetValue(key: id, value: out ImageEntity? image) ? image : null);
        }

        public Task<IReadOnlyList<ImageEntity>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
                Task.FromResult<IReadOnlyList<ImageEntity>>(
                        result: ids.Where(id => this._state.Images.ContainsKey(key: id)).Select(id => this._state.Images[id]).ToList());

        public Task<ImageEntity?> GetBySha256Async(string sha256Hash, CancellationToken cancellationToken) =>
                Task.FromResult(result: this._state.Images.Values.FirstOrDefault(image => !image.IsDeleted && image.Sha256Hash == sha256Hash));

        public Task<PagedResult<ImageEntity>> ListAsync(
                int page,
                int pageSize,
                string? searchTerm,
                string? albumPath,
                bool includeSubalbums,
                CancellationToken cancellationToken)
        {
                IEnumerable<ImageEntity> active = this._state.Images.Values.Where(image => !image.IsDeleted);
                if (!string.IsNullOrWhiteSpace(value: albumPath))
                {
                        string prefix = $"{albumPath}/";
                        active = includeSubalbums
                                ? active.Where(image => image.AlbumPath == albumPath || (image.AlbumPath != null && image.AlbumPath.StartsWith(value: prefix, comparisonType: StringComparison.OrdinalIgnoreCase)))
                                : active.Where(image => string.Equals(a: image.AlbumPath, b: albumPath, comparisonType: StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(value: searchTerm))
                {
                        string term = searchTerm.Trim();
                        active = active.Where(image =>
                                image.FileName.Contains(value: term, comparisonType: StringComparison.OrdinalIgnoreCase)
                                || (image.AltText != null && image.AltText.Contains(value: term, comparisonType: StringComparison.OrdinalIgnoreCase))
                                || (image.Tags != null && image.Tags.Contains(value: term, comparisonType: StringComparison.OrdinalIgnoreCase)));
                }

                List<ImageEntity> all = active.OrderBy(image => image.CreatedAt).ThenBy(image => image.Id).ToList();
                return Task.FromResult(result: new PagedResult<ImageEntity>
                {
                        Items = all.Skip(count: (page - 1) * pageSize).Take(count: pageSize).ToList(),
                        Page = page,
                        PageSize = pageSize,
                        TotalCount = all.Count
                });
        }

        public Task<IReadOnlyList<string>> ListChildAlbumPathsAsync(string? parentAlbumPath, CancellationToken cancellationToken) =>
                Task.FromResult<IReadOnlyList<string>>(result: []);

        public Task UpdateMetadataAsync(ImageEntity image, Guid expectedConcurrencyStamp, CancellationToken cancellationToken)
        {
                this._state.Images[image.Id] = image;
                return Task.CompletedTask;
        }

        public Task<bool> IsReferencedByAnyTemplateAsync(Guid imageId, CancellationToken cancellationToken) =>
                Task.FromResult(result: this._state.ReferencedImageIds.Contains(item: imageId));

        public Task AddAsync(ImageEntity metadata, ImageBinaryEntity binary, CancellationToken cancellationToken)
        {
                this._state.Images[metadata.Id] = metadata;
                this._state.Binaries[metadata.Id] = binary.Data;
                return Task.CompletedTask;
        }

        public Task DeleteAsync(ImageEntity image, Guid expectedConcurrencyStamp, CancellationToken cancellationToken)
        {
                if (!this._state.Images.ContainsKey(key: image.Id))
                {
                        throw new ConcurrencyConflictException($"Image '{image.Id}' was modified concurrently.");
                }

                this._state.Images[image.Id] = image;
                return Task.CompletedTask;
        }
}

