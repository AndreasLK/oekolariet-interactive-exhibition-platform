using HighscoreService.Application.Common;
using HighscoreService.Application.Interfaces;
using HighscoreService.Application.Interfaces.CommandHandlers;
using HighscoreService.Domain.Entities;
using HighscoreService.Facade.Commands;
using HighscoreService.Facade.Dtos;

namespace HighscoreService.Application.CommandHandlers
{
        /// <summary>
        /// Hub-only command handler that ingests synced highscore batches from exhibition Screens idempotently.
        /// </summary>
        /// <param name="hubSyncStore">Hub sync persistence store.</param>
        public sealed class ApplySyncBatchCommandHandler(IHubSyncStore hubSyncStore) : IApplySyncBatchCommandHandler
        {
                private readonly IHubSyncStore _hubSyncStore = hubSyncStore;

                /// <inheritdoc />
                public async Task<SyncBatchResultDto> HandleAsync(
                        ApplySyncBatchCommand command,
                        CancellationToken cancellationToken)
                {
                        if (command.Entries.Count == 0)
                        {
                                return new SyncBatchResultDto(0, 0);
                        }

                        // Reconstruct validated domain entities before persisting so sync payloads respect domain invariants.
                        IReadOnlyList<HighscoreEntity> domainEntries =
                        [
                                .. command.Entries.Select(HighscoreDtoMapper.ToDomainEntityForSync)
                        ];

                        int appliedCount = await this._hubSyncStore.ApplySyncedBatchAsync(
                                entries: domainEntries,
                                cancellationToken: cancellationToken);

                        return new SyncBatchResultDto(
                                ReceivedCount: command.Entries.Count,
                                AppliedCount: appliedCount);
                }
        }
}

