using ContentService.Application.Models;

namespace ContentService.Application.Interfaces;

/// <summary>
/// Coordinates stateless Dapr Pub/Sub scatter-gather queries across an arbitrary pool of <c>ContentService</c> instances without a central registry.
/// </summary>
public interface IPoolCoordinator
{
        /// <summary>
        /// Broadcasts an image-locate request via Dapr Pub/Sub and returns the first owning node's offer within the configured timeout (default 5s).
        /// </summary>
        /// <param name="imageId">The image GUID to locate across the pool.</param>
        /// <param name="cancellationToken">Propagates caller cancellation.</param>
        /// <returns>The owning node's <see cref="PoolNodeOffer"/>, or <see langword="null"/> if no node responds before the timeout.</returns>
        public Task<PoolNodeOffer?> LocateImageOwnerAsync(
                Guid imageId,
                CancellationToken cancellationToken);

        /// <summary>
        /// Broadcasts an upload-capacity request via Dapr Pub/Sub and returns the first healthy node with sufficient quota within the configured timeout.
        /// </summary>
        /// <param name="requiredBytes">The size in bytes of the image to upload.</param>
        /// <param name="cancellationToken">Propagates caller cancellation.</param>
        /// <returns>A healthy node's <see cref="PoolNodeOffer"/>, or <see langword="null"/> if no healthy node responds before the timeout.</returns>
        public Task<PoolNodeOffer?> RequestHealthyUploadOfferAsync(
                long requiredBytes,
                CancellationToken cancellationToken);

        /// <summary>
        /// Completes an in-flight scatter-gather request when a peer node posts its reply.
        /// </summary>
        /// <param name="correlationId">The unique correlation ID of the scatter-gather request.</param>
        /// <param name="offer">The peer node's offer payload.</param>
        public void CompletePendingRequest(
                Guid correlationId,
                PoolNodeOffer offer);

        /// <summary>
        /// Sends a pool reply back to the requesting caller, supporting either direct Dapr service invocation (<paramref name="replyToAppId"/>) or a Dapr Pub/Sub reply topic (<paramref name="replyTopic"/>).
        /// </summary>
        /// <param name="replyToAppId">Optional Dapr application ID of the requesting node for direct HTTP callback.</param>
        /// <param name="replyTopic">Optional Dapr Pub/Sub topic name when the caller prefers receiving offers over Pub/Sub.</param>
        /// <param name="correlationId">The correlation ID of the scatter-gather request.</param>
        /// <param name="offer">The local node's offer payload.</param>
        /// <param name="cancellationToken">Propagates cancellation.</param>
        /// <returns>A task representing the asynchronous reply delivery.</returns>
        public Task SendReplyAsync(
                string? replyToAppId,
                string? replyTopic,
                Guid correlationId,
                PoolNodeOffer offer,
                CancellationToken cancellationToken);
}

