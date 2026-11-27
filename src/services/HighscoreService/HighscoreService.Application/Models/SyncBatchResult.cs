namespace HighscoreService.Application.Models
{
        /// <summary>
        /// Result summary of an idempotent synchronization batch operation.
        /// </summary>
        public sealed record SyncBatchResult(
                int ReceivedCount,
                int AppliedCount);
}

