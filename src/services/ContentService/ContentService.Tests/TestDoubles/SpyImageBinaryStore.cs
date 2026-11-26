using ContentService.Application.Interfaces;

namespace ContentService.Tests.TestDoubles;

/// <summary>
/// Spy <see cref="IImageBinaryStore"/> counting <see cref="OpenReadAsync"/> invocations.
/// </summary>
internal sealed class SpyImageBinaryStore : IImageBinaryStore
{
        private readonly FakeImageRepository? _backingRepository;

        public SpyImageBinaryStore(FakeImageRepository? backingRepository = null)
        {
                this._backingRepository = backingRepository;
        }

        public int OpenReadCalls { get; private set; }

        public Task<Stream?> OpenReadAsync(Guid imageId, CancellationToken cancellationToken)
        {
                this.OpenReadCalls++;
                byte[]? data = this._backingRepository?.GetBinary(imageId: imageId);
                Stream? stream = data is null ? null : new MemoryStream(buffer: data, writable: false);
                return Task.FromResult(result: stream);
        }
}

