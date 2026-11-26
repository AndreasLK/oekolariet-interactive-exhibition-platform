using ContentService.Application.Interfaces;

namespace ContentService.Tests.Api.Fakes;

/// <summary>
/// In-memory <see cref="IStorageQuotaMonitor"/> test double for API pipeline tests.
/// </summary>
internal sealed class InMemoryStorageQuotaMonitor : IStorageQuotaMonitor
{
        private readonly InMemoryContentState _state;

        public InMemoryStorageQuotaMonitor(InMemoryContentState state) => this._state = state;

        public Task<long> GetUsedBytesAsync(CancellationToken cancellationToken)
        {
                long used = this._state.ForcedUsedBytesOverride
                        ?? this._state.Images.Values.Where(image => !image.IsDeleted).Sum(image => image.FileSizeBytes);
                return Task.FromResult(result: used);
        }
}

