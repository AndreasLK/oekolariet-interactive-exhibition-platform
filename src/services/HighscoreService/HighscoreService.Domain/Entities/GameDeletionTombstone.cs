using HighscoreService.Domain.Exceptions;

namespace HighscoreService.Domain.Entities
{
        /// <summary>
        /// Persistent tombstone recording that an EditorService game was deleted, preventing offline resurrection.
        /// </summary>
        public sealed class GameDeletionTombstone
        {
                /// <summary>
                /// Initializes a new instance of the <see cref="GameDeletionTombstone"/> class for EF Core.
                /// </summary>
                private GameDeletionTombstone()
                {
                }

                /// <summary>
                /// Gets the identifier of the deleted game.
                /// </summary>
                public Guid GameId { get; private set; }

                /// <summary>
                /// Gets the UTC timestamp when the game was deleted.
                /// </summary>
                public DateTimeOffset DeletedAtUtc { get; private set; }

                /// <summary>
                /// Gets the monotonic Hub sequence assigned to this tombstone.
                /// </summary>
                public long SyncSequence { get; private set; }

                /// <summary>
                /// Creates a validated <see cref="GameDeletionTombstone"/>.
                /// </summary>
                /// <param name="gameId">The deleted game identifier.</param>
                /// <param name="deletedAtUtc">The UTC deletion timestamp.</param>
                /// <param name="syncSequence">Optional monotonic sync sequence.</param>
                /// <returns>A validated tombstone instance.</returns>
                public static GameDeletionTombstone Create(
                        Guid gameId,
                        DateTimeOffset deletedAtUtc,
                        long syncSequence = 0)
                {
                        // Require a non-empty game identifier so tombstones always map to a real game.
                        if (gameId == Guid.Empty)
                        {
                                throw new DomainValidationException("GameId cannot be empty.");
                        }

                        // Normalize timestamp to UTC for consistent comparisons across Hub and Screen nodes.
                        return new GameDeletionTombstone
                        {
                                GameId = gameId,
                                DeletedAtUtc = deletedAtUtc.ToUniversalTime(),
                                SyncSequence = syncSequence
                        };
                }

                /// <summary>
                /// Advances the deletion timestamp if a newer deletion event is received.
                /// </summary>
                /// <param name="deletedAtUtc">The UTC deletion timestamp.</param>
                /// <param name="syncSequence">The updated sync sequence.</param>
                public void UpdateDeletion(
                        DateTimeOffset deletedAtUtc,
                        long syncSequence)
                {
                        // Only advance timestamp and sequence monotonically so stale sync replays cannot regress state.
                        DateTimeOffset utc = deletedAtUtc.ToUniversalTime();
                        if (utc > this.DeletedAtUtc)
                        {
                                this.DeletedAtUtc = utc;
                        }

                        if (syncSequence > this.SyncSequence)
                        {
                                this.SyncSequence = syncSequence;
                        }
                }
        }
}

