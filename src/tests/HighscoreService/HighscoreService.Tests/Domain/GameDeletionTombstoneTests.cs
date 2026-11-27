using HighscoreService.Domain.Entities;
using HighscoreService.Domain.Exceptions;

namespace HighscoreService.Tests.Domain
{
        /// <summary>
        /// Unit tests for <see cref="GameDeletionTombstone"/> validation and monotonic update rules.
        /// </summary>
        public sealed class GameDeletionTombstoneTests
        {
                /// <summary>
                /// Verifies <see cref="GameDeletionTombstone.Create"/> rejects <see cref="Guid.Empty"/> and normalizes timestamps to UTC.
                /// </summary>
                [Fact]
                public void Create_RejectsEmptyGameId_AndNormalizesTimestampToUtc()
                {
                        // Reject empty game identifier.
                        Assert.Throws<DomainValidationException>(() =>
                                GameDeletionTombstone.Create(Guid.Empty, DateTimeOffset.UtcNow));

                        DateTimeOffset localTime = new(2026, 11, 27, 12, 0, 0, TimeSpan.FromHours(1));
                        GameDeletionTombstone tombstone = GameDeletionTombstone.Create(
                                gameId: Guid.CreateVersion7(),
                                deletedAtUtc: localTime,
                                syncSequence: 7);

                        Assert.Equal(TimeSpan.Zero, tombstone.DeletedAtUtc.Offset);
                        Assert.Equal(new DateTimeOffset(2026, 11, 27, 11, 0, 0, TimeSpan.Zero), tombstone.DeletedAtUtc);
                        Assert.Equal(7, tombstone.SyncSequence);
                }

                /// <summary>
                /// Verifies <see cref="GameDeletionTombstone.UpdateDeletion"/> only advances timestamps and sequences monotonically.
                /// </summary>
                [Fact]
                public void UpdateDeletion_IgnoresStaleTimestampsAndSequences()
                {
                        // Arrange
                        DateTimeOffset initialTime = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);
                        GameDeletionTombstone tombstone = GameDeletionTombstone.Create(
                                gameId: Guid.CreateVersion7(),
                                deletedAtUtc: initialTime,
                                syncSequence: 10);

                        // Act: Apply stale values first, then newer values.
                        tombstone.UpdateDeletion(initialTime.AddMinutes(-5), syncSequence: 5);
                        Assert.Equal(initialTime, tombstone.DeletedAtUtc);
                        Assert.Equal(10, tombstone.SyncSequence);

                        DateTimeOffset newerTime = initialTime.AddMinutes(5);
                        tombstone.UpdateDeletion(newerTime, syncSequence: 15);

                        // Assert
                        Assert.Equal(newerTime, tombstone.DeletedAtUtc);
                        Assert.Equal(15, tombstone.SyncSequence);
                }
        }
}

