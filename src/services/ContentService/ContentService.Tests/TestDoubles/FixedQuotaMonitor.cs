using ContentService.Application.Interfaces;

namespace ContentService.Tests.TestDoubles;

/// <summary>
/// Fixed quota monitor returning a constant byte usage value.
/// </summary>
internal sealed class FixedQuotaMonitor : IStorageQuotaMonitor
{
        private readonly long _usedBytes;

        public FixedQuotaMonitor(long usedBytes) => this._usedBytes = usedBytes;

        public Task<long> GetUsedBytesAsync(CancellationToken cancellationToken) => Task.FromResult(result: this._usedBytes);
}

