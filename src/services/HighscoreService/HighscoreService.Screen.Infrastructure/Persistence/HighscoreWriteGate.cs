namespace HighscoreService.Screen.Infrastructure.Persistence
{
        /// <summary>
        /// Serializes in-process SQLite write operations in non-WAL DELETE mode to prevent SQLITE_BUSY lock collisions.
        /// </summary>
        /// <remarks>
        /// NON-STANDARD DECISION: Uses an explicit in-process <see cref="SemaphoreSlim"/> write gate instead of
        /// SQLite WAL mode.
        /// <para>
        /// Pros: Eliminates <c>-wal</c>/<c>-shm</c> sidecar files and guarantees immediate single-file durability
        /// when exhibition kiosks are hard-powered off at closing time.
        /// Cons: Writes are serialized per Screen process rather than concurrent across readers/writers.
        /// </para>
        /// </remarks>
        public sealed class HighscoreWriteGate : IDisposable
        {
                private readonly SemaphoreSlim _semaphore = new(1, 1);

                /// <summary>
                /// Acquires the write gate asynchronously.
                /// </summary>
                /// <param name="cancellationToken">Cancellation token.</param>
                public Task WaitAsync(CancellationToken cancellationToken)
                {
                        // Serialize SQLite writes so non-WAL DELETE mode never encounters concurrent writer locks.
                        return this._semaphore.WaitAsync(cancellationToken);
                }

                /// <summary>
                /// Releases the write gate.
                /// </summary>
                public void Release()
                {
                        // Allow the next queued SQLite writer to proceed.
                        this._semaphore.Release();
                }

                /// <inheritdoc />
                public void Dispose()
                {
                        // Release the underlying semaphore handle on container shutdown.
                        this._semaphore.Dispose();
                }
        }
}

