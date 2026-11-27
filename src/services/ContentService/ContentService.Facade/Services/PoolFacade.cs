using ContentService.Application.Interfaces;
using ContentService.Application.Models;
using ContentService.Application.Options;
using ContentService.Facade.Dtos;
using ContentService.Facade.Interfaces;
using ContentService.Facade.Mapping;
using Microsoft.Extensions.Options;

namespace ContentService.Facade.Services;

/// <summary>
/// Default <see cref="IPoolFacade"/> delegating stateless Dapr pool coordination to <see cref="IPoolCoordinator"/>.
/// </summary>
internal sealed class PoolFacade : IPoolFacade
{
        /// <summary>
        /// Prefix applied to normalized storage node IDs when constructing Dapr application IDs.
        /// </summary>
        private const string DAPR_APP_ID_PREFIX = "contentservice-";

        /// <summary>
        /// Application-layer pool coordinator handling Dapr Pub/Sub scatter-gather and replies.
        /// </summary>
        private readonly IPoolCoordinator _poolCoordinator;

        /// <summary>
        /// Configured local storage options providing this node's identity and public base URL.
        /// </summary>
        private readonly ContentStorageOptions _storageOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="PoolFacade"/> class.
        /// </summary>
        /// <param name="poolCoordinator">The application pool coordinator.</param>
        /// <param name="storageOptions">Configured local storage options.</param>
        /// <exception cref="ArgumentNullException">Thrown when a dependency is <see langword="null"/>.</exception>
        public PoolFacade(
                IPoolCoordinator poolCoordinator,
                IOptions<ContentStorageOptions> storageOptions)
        {
                ArgumentNullException.ThrowIfNull(poolCoordinator);
                ArgumentNullException.ThrowIfNull(storageOptions);

                this._poolCoordinator = poolCoordinator;
                this._storageOptions = storageOptions.Value;
        }

        /// <inheritdoc />
        public async Task<PoolNodeOfferDto?> RequestHealthyUploadOfferAsync(
                long requiredBytes,
                CancellationToken cancellationToken = default)
        {
                // Broadcast the upload capacity request and map any winning offer to a Facade DTO.
                PoolNodeOffer? offer = await this._poolCoordinator.RequestHealthyUploadOfferAsync(
                        requiredBytes: requiredBytes,
                        cancellationToken: cancellationToken);

                return offer is not null
                        ? FacadeMapper.ToDto(offer: offer)
                        : null;
        }

        /// <inheritdoc />
        public void CompletePendingRequest(
                Guid correlationId,
                PoolNodeOfferDto offer)
        {
                ArgumentNullException.ThrowIfNull(offer);

                // Translate the Facade DTO to the Application model before completing the awaiting task.
                this._poolCoordinator.CompletePendingRequest(
                        correlationId: correlationId,
                        offer: FacadeMapper.ToApplication(offer: offer));
        }

        /// <inheritdoc />
        public Task SendLocalOfferReplyAsync(
                string? replyToAppId,
                string? replyTopic,
                Guid correlationId,
                long availableBytes,
                CancellationToken cancellationToken = default)
        {
                // Build this node's offer from local storage options and send it to the requester.
                string normalizedNodeId = this._storageOptions.StorageNodeId.Trim().ToLowerInvariant();
                PoolNodeOffer localOffer = new()
                {
                        StorageNodeId = normalizedNodeId,
                        DaprAppId = $"{DAPR_APP_ID_PREFIX}{normalizedNodeId}",
                        PublicBaseUrl = this._storageOptions.PublicBaseUrl,
                        AvailableBytes = availableBytes
                };

                return this._poolCoordinator.SendReplyAsync(
                        replyToAppId: replyToAppId,
                        replyTopic: replyTopic,
                        correlationId: correlationId,
                        offer: localOffer,
                        cancellationToken: cancellationToken);
        }
}

