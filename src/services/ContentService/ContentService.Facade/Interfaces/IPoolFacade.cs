using ContentService.Facade.Dtos;

namespace ContentService.Facade.Interfaces;

/// <summary>
/// Facade contract for stateless storage-pool scatter-gather coordination so API endpoints stay decoupled from Application internals.
/// </summary>
public interface IPoolFacade
{
        /// <summary>
        /// Broadcasts an upload-capacity request to the pool and returns the first healthy node's offer within the timeout.
        /// </summary>
        /// <param name="requiredBytes">Minimum remaining bytes required for the upload.</param>
        /// <param name="cancellationToken">Propagates cancellation.</param>
        /// <returns>The winning <see cref="PoolNodeOfferDto"/>, or <see langword="null"/> if no healthy node replied in time.</returns>
        public Task<PoolNodeOfferDto?> RequestHealthyUploadOfferAsync(
                long requiredBytes,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Completes an in-flight scatter-gather request when a peer node posts a direct callback reply.
        /// </summary>
        /// <param name="correlationId">The correlation identifier of the pending scatter-gather request.</param>
        /// <param name="offer">The peer node's offer payload.</param>
        public void CompletePendingRequest(
                Guid correlationId,
                PoolNodeOfferDto offer);

        /// <summary>
        /// Sends this node's local capacity offer back to the requesting node or reply topic.
        /// </summary>
        /// <param name="replyToAppId">Optional target Dapr application ID for direct callback.</param>
        /// <param name="replyTopic">Optional reply Pub/Sub topic.</param>
        /// <param name="correlationId">The correlation identifier echoed to the requester.</param>
        /// <param name="availableBytes">Remaining quota bytes on this node.</param>
        /// <param name="cancellationToken">Propagates cancellation.</param>
        public Task SendLocalOfferReplyAsync(
                string? replyToAppId,
                string? replyTopic,
                Guid correlationId,
                long availableBytes,
                CancellationToken cancellationToken = default);
}

