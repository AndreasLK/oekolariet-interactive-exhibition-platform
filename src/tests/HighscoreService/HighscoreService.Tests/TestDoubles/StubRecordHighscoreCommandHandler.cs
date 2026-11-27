using HighscoreService.Application.Interfaces.CommandHandlers;
using HighscoreService.Domain.Exceptions;
using HighscoreService.Facade.Commands;
using HighscoreService.Facade.Dtos;

namespace HighscoreService.Tests.TestDoubles
{
        /// <summary>
        /// In-memory stub for <see cref="IRecordHighscoreCommandHandler"/> used in reverse-proxy integration tests.
        /// </summary>
        internal sealed class StubRecordHighscoreCommandHandler : IRecordHighscoreCommandHandler
        {
                /// <inheritdoc />
                public Task<HighscoreDto> HandleAsync(RecordHighscoreCommand command, CancellationToken cancellationToken = default)
                {
                        // Reject blank player names to exercise ProblemDetails + CORS error handling.
                        if (string.IsNullOrWhiteSpace(command.Name))
                        {
                                throw new DomainValidationException("Player name is required.");
                        }

                        // Echo back a valid HighscoreDto without touching a database.
                        HighscoreDto dto = new(
                                Id: command.Id ?? Guid.NewGuid(),
                                Name: command.Name,
                                Age: command.Age,
                                Score: command.Score,
                                SenderMacAddress: command.SenderMacAddress,
                                GameId: command.GameId,
                                PlayedAtUtc: command.PlayedAtUtc ?? DateTimeOffset.UtcNow,
                                IsTimedOut: command.IsTimedOut,
                                RankingAtTimeOfScoring: 1,
                                IsDeleted: false,
                                DeletedAtUtc: null,
                                SyncSequence: 1);

                        return Task.FromResult(dto);
                }
        }
}

