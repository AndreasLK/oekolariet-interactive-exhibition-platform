using ContentService.Application.Common;
using ContentService.Application.Interfaces;
using ContentService.Domain.Entities;

namespace ContentService.Tests.TestDoubles;

/// <summary>
/// Minimal in-memory <see cref="IImageRepository"/> test double for unit tests.
/// </summary>
internal sealed class FakeImageRepository : IImageRepository
{
        private readonly Dictionary<Guid, ImageEntity> _images = [];
        private readonly Dictionary<Guid, byte[]> _binaries = [];

        public void Seed(ImageEntity image, byte[]? binaryBytes)
        {
                this._images[image.Id] = image;
                if (binaryBytes is not null)
                {
                        this._binaries[image.Id] = binaryBytes;
                }
        }

        public byte[]? GetBinary(Guid imageId) =>
                this._binaries.TryGetValue(key: imageId, value: out byte[]? data) ? data : null;

        public Task<ImageEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
                Task.FromResult(result: this._images.TryGetValue(key: id, value: out ImageEntity? image) ? image : null);

        public Task<IReadOnlyList<ImageEntity>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
                Task.FromResult<IReadOnlyList<ImageEntity>>(
                        result: ids.Where(id => this._images.ContainsKey(key: id)).Select(id => this._images[id]).ToList());

        public Task<ImageEntity?> GetBySha256Async(string sha256Hash, CancellationToken cancellationToken) =>
                Task.FromResult(result: this._images.Values.FirstOrDefault(image => !image.IsDeleted && image.Sha256Hash == sha256Hash));

        public Task<PagedResult<ImageEntity>> ListAsync(
                int page,
                int pageSize,
                string? searchTerm,
                string? albumPath,
                bool includeSubalbums,
                CancellationToken cancellationToken) =>
                Task.FromResult(result: new PagedResult<ImageEntity>
                {
                        Items = this._images.Values.Where(image => !image.IsDeleted).ToList(),
                        Page = page,
                        PageSize = pageSize,
                        TotalCount = this._images.Values.Count(image => !image.IsDeleted)
                });

        public Task<IReadOnlyList<string>> ListChildAlbumPathsAsync(string? parentAlbumPath, CancellationToken cancellationToken) =>
                Task.FromResult<IReadOnlyList<string>>(result: []);

        public Task UpdateMetadataAsync(ImageEntity image, Guid expectedConcurrencyStamp, CancellationToken cancellationToken) =>
                Task.CompletedTask;

        public Task<bool> IsReferencedByAnyTemplateAsync(Guid imageId, CancellationToken cancellationToken) =>
                Task.FromResult(result: false);

        public Task AddAsync(ImageEntity metadata, ImageBinaryEntity binary, CancellationToken cancellationToken)
        {
                this._images[metadata.Id] = metadata;
                this._binaries[metadata.Id] = binary.Data;
                return Task.CompletedTask;
        }

        public Task DeleteAsync(ImageEntity image, Guid expectedConcurrencyStamp, CancellationToken cancellationToken) =>
                Task.CompletedTask;
}

