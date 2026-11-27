namespace HighscoreService.Facade.Commands
{
        /// <summary>
        /// Command to record a new highscore or abandoned-round entry.
        /// </summary>
        /// <param name="Id">Optional client-generated UUIDv7 for idempotent retries.</param>
        /// <param name="Name">Optional player display name.</param>
        /// <param name="Age">Optional player age.</param>
        /// <param name="Score">Achieved score.</param>
        /// <param name="SenderMacAddress">MAC address of the originating exhibition device.</param>
        /// <param name="GameId">Identifier of the game that was played.</param>
        /// <param name="PlayedAtUtc">Optional UTC timestamp when the round finished.</param>
        /// <param name="IsTimedOut">Whether the round timed out before completion.</param>
        public sealed record RecordHighscoreCommand(
                Guid? Id,
                string? Name,
                int? Age,
                int Score,
                string SenderMacAddress,
                Guid GameId,
                DateTimeOffset? PlayedAtUtc,
                bool IsTimedOut);
}

