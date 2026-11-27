using System.Collections.Concurrent;
using ContentService.Application.Models;

namespace ContentService.Infrastructure.Nodes;

/// <summary>
/// Tracks in-flight scatter-gather pool requests by correlation ID so peer replies resolve the awaiting caller.
/// </summary>
internal sealed class PendingPoolReplyRegistry
{
        /// <summary>
        /// Thread-safe map of active correlation IDs to their awaiting completion sources.
        /// </summary>
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<PoolNodeOffer>> _pendingRequests = new();

        /// <summary>
        /// Registers a new pending request for <paramref name="correlationId"/>.
        /// </summary>
        /// <param name="correlationId">The unique correlation identifier for the scatter-gather broadcast.</param>
        /// <returns>The <see cref="TaskCompletionSource{TResult}"/> that completes when a peer replies.</returns>
        public TaskCompletionSource<PoolNodeOffer> Register(Guid correlationId)
        {
                // Run continuations asynchronously so the HTTP callback thread never executes caller logic inline.
                TaskCompletionSource<PoolNodeOffer> tcs = new(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);
                this._pendingRequests[correlationId] = tcs;
                return tcs;
        }

        /// <summary>
        /// Completes the awaiting request for <paramref name="correlationId"/> with the winning <see cref="PoolNodeOffer"/>.
        /// </summary>
        /// <param name="correlationId">The correlation identifier echoed by the replying peer.</param>
        /// <param name="offer">The peer's offer payload.</param>
        public void Complete(Guid correlationId, PoolNodeOffer offer)
        {
                ArgumentNullException.ThrowIfNull(offer);

                // Atomically remove the entry so only the first peer reply wins the scatter-gather race.
                if (this._pendingRequests.TryRemove(key: correlationId, value: out TaskCompletionSource<PoolNodeOffer>? tcs))
                {
                        tcs.TrySetResult(result: offer);
                }
        }

        /// <summary>
        /// Removes any lingering entry for <paramref name="correlationId"/> after completion, timeout, or cancellation.
        /// </summary>
        /// <param name="correlationId">The correlation identifier to clean up.</param>
        public void Remove(Guid correlationId)
        {
                // Ensure timed-out or cancelled broadcasts do not leak entries in the dictionary.
                this._pendingRequests.TryRemove(key: correlationId, value: out _);
        }
}

