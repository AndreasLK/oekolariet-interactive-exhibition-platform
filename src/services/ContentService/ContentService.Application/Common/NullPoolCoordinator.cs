using ContentService.Application.Interfaces;
using ContentService.Application.Models;

namespace ContentService.Application.Common;

/// <summary>
/// Fallback <see cref="IPoolCoordinator"/> used when Infrastructure (Dapr) is not registered (e.g., isolated unit tests).
/// </summary>
internal sealed class NullPoolCoordinator : IPoolCoordinator
{
        /// <inheritdoc />
        public Task<PoolNodeOffer?> LocateImageOwnerAsync(
                Guid imageId,
                CancellationToken cancellationToken) =>
                Task.FromResult<PoolNodeOffer?>(null);

        /// <inheritdoc />
        public Task<PoolNodeOffer?> RequestHealthyUploadOfferAsync(
                long requiredBytes,
                CancellationToken cancellationToken) =>
                Task.FromResult<PoolNodeOffer?>(null);

        /// <inheritdoc />
        public void CompletePendingRequest(
                Guid correlationId,
                PoolNodeOffer offer)
        {
        }

        /// <inheritdoc />
        public Task SendReplyAsync(
                string? replyToAppId,
                string? replyTopic,
                Guid correlationId,
                PoolNodeOffer offer,
                CancellationToken cancellationToken) =>
                Task.CompletedTask;
}

