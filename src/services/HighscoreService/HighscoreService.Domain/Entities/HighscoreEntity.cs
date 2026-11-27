using HighscoreService.Domain.Common;
using HighscoreService.Domain.Exceptions;
using HighscoreService.Domain.ValueObjects;

namespace HighscoreService.Domain.Entities
{
        /// <summary>
        /// Represents a single recorded game play / highscore entry from an exhibition screen.
        /// </summary>
        public sealed class HighscoreEntity
        {
                /// <summary>
                /// Initializes a new instance of the <see cref="HighscoreEntity"/> class for EF Core materialization.
                /// </summary>
                private HighscoreEntity()
                {
                }

                /// <summary>
                /// Gets the globally unique identifier generated at the origin screen or API boundary.
                /// </summary>
                public Guid Id { get; private set; }

                /// <summary>
                /// Gets the optional player name (null for anonymous or half-played statistic entries).
                /// </summary>
                public string? Name { get; private set; }

                /// <summary>
                /// Gets the optional player age in years (0 to 200).
                /// </summary>
                public int? Age { get; private set; }

                /// <summary>
                /// Gets the non-negative score achieved in the round.
                /// </summary>
                public int Score { get; private set; }

                /// <summary>
                /// Gets the normalized MAC address of the screen server that sent this score.
                /// </summary>
                public string SenderMacAddress { get; private set; } = string.Empty;

                /// <summary>
                /// Gets the identifier of the game in EditorService.
                /// </summary>
                public Guid GameId { get; private set; }

                /// <summary>
                /// Gets the UTC timestamp when the round was played.
                /// </summary>
                public DateTimeOffset PlayedAtUtc { get; private set; }

                /// <summary>
                /// Gets the 1-based rank within the configured rolling window at the time of scoring.
                /// </summary>
                public int? RankingAtTimeOfScoring { get; private set; }

                /// <summary>
                /// Gets a value indicating whether the round ended due to timeout/abandonment rather than normal completion.
                /// </summary>
                public bool IsTimedOut { get; private set; }

                /// <summary>
                /// Gets a value indicating whether this record has been soft-deleted (e.g. game deleted or moderated).
                /// </summary>
                public bool IsDeleted { get; private set; }

                /// <summary>
                /// Gets the UTC timestamp when this record was soft-deleted, if applicable.
                /// </summary>
                public DateTimeOffset? DeletedAtUtc { get; private set; }

                /// <summary>
                /// Gets the monotonic server-assigned sync sequence used for reliable incremental pull synchronization.
                /// </summary>
                public long SyncSequence { get; private set; }

                /// <summary>
                /// Creates and validates a new <see cref="HighscoreEntity"/>.
                /// </summary>
                /// <param name="id">Globally unique identifier (must not be <see cref="Guid.Empty"/>).</param>
                /// <param name="name">Optional player name.</param>
                /// <param name="age">Optional player age (0 to 200).</param>
                /// <param name="score">Non-negative score.</param>
                /// <param name="senderMacAddress">MAC address of the sending screen server.</param>
                /// <param name="gameId">Identifier of the game.</param>
                /// <param name="playedAtUtc">UTC timestamp when the round was played.</param>
                /// <param name="isTimedOut">Whether the round timed out / was abandoned.</param>
                /// <param name="rankingAtTimeOfScoring">Optional pre-computed rank at time of scoring.</param>
                /// <returns>A validated <see cref="HighscoreEntity"/>.</returns>
                public static HighscoreEntity Create(
                        Guid id,
                        string? name,
                        int? age,
                        int score,
                        string senderMacAddress,
                        Guid gameId,
                        DateTimeOffset playedAtUtc,
                        bool isTimedOut = false,
                        int? rankingAtTimeOfScoring = null)
                {
                        ValidateIdentity(id, gameId);
                        string? normalizedName = NormalizeAndValidateName(name);
                        ValidateScoreAndNameCombination(normalizedName, score);
                        ValidateAge(age);

                        MacAddress validatedMac = MacAddress.Parse(senderMacAddress);

                        if (rankingAtTimeOfScoring.HasValue && rankingAtTimeOfScoring.Value < DomainConstants.MIN_RANK)
                        {
                                throw new DomainValidationException("Ranking at time of scoring must be at least 1.");
                        }

                        return new HighscoreEntity
                        {
                                Id = id,
                                Name = normalizedName,
                                Age = age,
                                Score = score,
                                SenderMacAddress = validatedMac.Value,
                                GameId = gameId,
                                PlayedAtUtc = playedAtUtc.ToUniversalTime(),
                                IsTimedOut = isTimedOut,
                                RankingAtTimeOfScoring = rankingAtTimeOfScoring,
                                IsDeleted = false,
                                DeletedAtUtc = null,
                                SyncSequence = 0
                        };
                }

                /// <summary>
                /// Records the 1-based ranking within the rolling window at the time of scoring.
                /// </summary>
                /// <param name="rank">The 1-based rank.</param>
                public void AssignRankingAtTimeOfScoring(int rank)
                {
                        if (rank < DomainConstants.MIN_RANK)
                        {
                                throw new DomainValidationException("Ranking at time of scoring must be at least 1.");
                        }

                        this.RankingAtTimeOfScoring = rank;
                }

                /// <summary>
                /// Assigns the server-side monotonic sync sequence after persistence.
                /// </summary>
                /// <param name="syncSequence">The positive monotonic sequence value.</param>
                public void AssignSyncSequence(long syncSequence)
                {
                        if (syncSequence <= 0)
                        {
                                throw new DomainValidationException("Sync sequence must be positive.");
                        }

                        this.SyncSequence = syncSequence;
                }

                /// <summary>
                /// Marks this highscore entry as soft-deleted so deletions propagate across synced instances.
                /// </summary>
                /// <param name="deletedAtUtc">The UTC timestamp of deletion.</param>
                public void MarkDeleted(DateTimeOffset deletedAtUtc)
                {
                        // Keep the original deletion timestamp if already deleted so tombstones stay idempotent.
                        if (this.IsDeleted)
                        {
                                return;
                        }

                        this.IsDeleted = true;
                        this.DeletedAtUtc = deletedAtUtc.ToUniversalTime();
                }

                private static void ValidateIdentity(Guid id, Guid gameId)
                {
                        if (id == Guid.Empty)
                        {
                                throw new DomainValidationException("Highscore Id cannot be empty.");
                        }

                        if (gameId == Guid.Empty)
                        {
                                throw new DomainValidationException("GameId cannot be empty.");
                        }
                }

                private static string? NormalizeAndValidateName(string? name)
                {
                        if (string.IsNullOrWhiteSpace(name))
                        {
                                return null;
                        }

                        string trimmed = name.Trim();
                        if (trimmed.Length > DomainConstants.MAX_PLAYER_NAME_LENGTH)
                        {
                                throw new DomainValidationException(
                                        $"Player name cannot exceed {DomainConstants.MAX_PLAYER_NAME_LENGTH} characters.");
                        }

                        return trimmed;
                }

                private static void ValidateScoreAndNameCombination(string? normalizedName, int score)
                {
                        if (score < DomainConstants.MIN_SCORE)
                        {
                                throw new DomainValidationException("Score cannot be negative.");
                        }

                        // Half-played/anonymous entries must still have either a name or a positive score.
                        if (normalizedName is null && score == 0)
                        {
                                throw new DomainValidationException(
                                        "Entry cannot have both an empty name and a score of 0.");
                        }
                }

                private static void ValidateAge(int? age)
                {
                        if (!age.HasValue)
                        {
                                return;
                        }

                        if (age.Value < DomainConstants.MIN_PLAYER_AGE || age.Value > DomainConstants.MAX_PLAYER_AGE)
                        {
                                throw new DomainValidationException(
                                        $"Age must be between {DomainConstants.MIN_PLAYER_AGE} and {DomainConstants.MAX_PLAYER_AGE}.");
                        }
                }
        }
}

