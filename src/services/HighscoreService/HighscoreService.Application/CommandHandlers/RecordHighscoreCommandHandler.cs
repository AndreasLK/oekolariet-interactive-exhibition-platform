using HighscoreService.Application.Common;
using HighscoreService.Application.Interfaces;
using HighscoreService.Application.Interfaces.CommandHandlers;
using HighscoreService.Application.Models;
using HighscoreService.Application.Options;
using HighscoreService.Domain.Entities;
using HighscoreService.Facade.Commands;
using HighscoreService.Facade.Dtos;
using Microsoft.Extensions.Options;

namespace HighscoreService.Application.CommandHandlers
{
        /// <summary>
        /// Validates and records a new highscore entry idempotently, computing its rolling-window rank.
        /// </summary>
        /// <param name="store">Highscore persistence store.</param>
        /// <param name="options">Ranking configuration options.</param>
        /// <param name="timeProvider">Server clock provider.</param>
        public sealed class RecordHighscoreCommandHandler(
                IHighscoreStore store,
                IOptions<RankingOptions> options,
                TimeProvider timeProvider) : IRecordHighscoreCommandHandler
        {
                private readonly IHighscoreStore _store = store;
                private readonly RankingOptions _options = options.Value;
                private readonly TimeProvider _timeProvider = timeProvider;

                /// <inheritdoc />
                public async Task<HighscoreDto> HandleAsync(
                        RecordHighscoreCommand command,
                        CancellationToken cancellationToken)
                {
                        // Generate a UUIDv7 and server UTC timestamp when the caller omits them so retries remain idempotent.
                        Guid entryId = command.Id ?? Guid.CreateVersion7();
                        DateTimeOffset playedAtUtc = (command.PlayedAtUtc ?? this._timeProvider.GetUtcNow()).ToUniversalTime();

                        HighscoreEntity entity = HighscoreEntity.Create(
                                id: entryId,
                                name: command.Name,
                                age: command.Age,
                                score: command.Score,
                                senderMacAddress: command.SenderMacAddress,
                                gameId: command.GameId,
                                playedAtUtc: playedAtUtc,
                                isTimedOut: command.IsTimedOut);

                        HighscoreInsertOutcome outcome = await this._store.AddIdempotentAsync(
                                entity: entity,
                                rollingRankingWindowHours: this._options.RollingRankingWindowHours,
                                cancellationToken: cancellationToken);

                        return HighscoreDtoMapper.ToDto(outcome.Entity);
                }
        }
}

