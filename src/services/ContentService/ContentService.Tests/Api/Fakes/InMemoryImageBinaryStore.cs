using ContentService.Application.Interfaces;
using ContentService.Domain.Entities;

namespace ContentService.Tests.Api.Fakes;

/// <summary>
/// In-memory <see cref="IImageBinaryStore"/> test double for API pipeline tests.
/// </summary>
internal sealed class InMemoryImageBinaryStore : IImageBinaryStore
{
        private readonly InMemoryContentState _state;

        public InMemoryImageBinaryStore(InMemoryContentState state) => this._state = state;

        public Task<Stream?> OpenReadAsync(Guid imageId, CancellationToken cancellationToken)
        {
                if (!this._state.Images.TryGetValue(key: imageId, value: out ImageEntity? image)
                        || image.IsDeleted
                        || !this._state.Binaries.TryGetValue(key: imageId, value: out byte[]? data))
                {
                        return Task.FromResult<Stream?>(result: null);
                }

                Stream stream = new MemoryStream(buffer: data, writable: false);
                return Task.FromResult<Stream?>(result: stream);
        }
}

