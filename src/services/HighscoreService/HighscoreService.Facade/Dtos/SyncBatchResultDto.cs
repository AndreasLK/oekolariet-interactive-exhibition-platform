namespace HighscoreService.Facade.Dtos
{
        /// <summary>
        /// Facade DTO summarizing an idempotent sync batch operation.
        /// </summary>
        public sealed record SyncBatchResultDto(
                int ReceivedCount,
                int AppliedCount);
}

