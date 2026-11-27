using HighscoreService.Domain.Entities;
using HighscoreService.Domain.Exceptions;

namespace HighscoreService.Tests.Domain
{
        /// <summary>
        /// Unit tests for <see cref="HighscoreEntity"/> domain validation and invariants.
        /// </summary>
        public sealed class HighscoreEntityTests
        {
                /// <summary>
                /// Verifies player names are trimmed and hyphenated MAC addresses normalize to uppercase colon format.
                /// </summary>
                [Fact]
                public void Create_NormalizesMacAddressAndTrimsName()
                {
                        // Exercise input normalization on both player name and MAC address.
                        HighscoreEntity entity = HighscoreEntity.Create(
                                id: Guid.NewGuid(),
                                name: "  Alice  ",
                                age: 12,
                                score: 250,
                                senderMacAddress: "aa-bb-cc-dd-ee-ff",
                                gameId: Guid.NewGuid(),
                                playedAtUtc: DateTimeOffset.UtcNow);

                        Assert.Equal("Alice", entity.Name);
                        Assert.Equal("AA:BB:CC:DD:EE:FF", entity.SenderMacAddress);
                }

                /// <summary>
                /// Verifies unnamed/blank-name entries with zero score are rejected by domain invariants.
                /// </summary>
                [Fact]
                public void Create_RejectsNullNameWithZeroScore()
                {
                        // Blank name with score 0 violates the meaningful-play domain rule.
                        Assert.Throws<DomainValidationException>(() => HighscoreEntity.Create(
                                id: Guid.NewGuid(),
                                name: "   ",
                                age: 10,
                                score: 0,
                                senderMacAddress: "AA:BB:CC:DD:EE:FF",
                                gameId: Guid.NewGuid(),
                                playedAtUtc: DateTimeOffset.UtcNow));
                }

                /// <summary>
                /// Verifies anonymous timed-out entries are permitted when the score is positive.
                /// </summary>
                [Fact]
                public void Create_AllowsNullNameWhenScoreIsPositive()
                {
                        // Timed-out plays with score > 0 are retained for abandonment statistics.
                        HighscoreEntity entity = HighscoreEntity.Create(
                                id: Guid.NewGuid(),
                                name: null,
                                age: null,
                                score: 40,
                                senderMacAddress: "AA:BB:CC:DD:EE:FF",
                                gameId: Guid.NewGuid(),
                                playedAtUtc: DateTimeOffset.UtcNow,
                                isTimedOut: true);

                        Assert.Null(entity.Name);
                        Assert.True(entity.IsTimedOut);
                }

                /// <summary>
                /// Verifies ages outside the valid range are rejected.
                /// </summary>
                /// <param name="invalidAge">Out-of-range age value.</param>
                [Theory]
                [InlineData(-1)]
                [InlineData(201)]
                public void Create_RejectsOutOfRangeAge(int invalidAge)
                {
                        // Reject negative or unrealistically large ages.
                        Assert.Throws<DomainValidationException>(() => HighscoreEntity.Create(
                                id: Guid.NewGuid(),
                                name: "Bob",
                                age: invalidAge,
                                score: 10,
                                senderMacAddress: "AA:BB:CC:DD:EE:FF",
                                gameId: Guid.NewGuid(),
                                playedAtUtc: DateTimeOffset.UtcNow));
                }

                /// <summary>
                /// Verifies empty, malformed, or all-zero MAC addresses are rejected.
                /// </summary>
                /// <param name="invalidMac">Invalid MAC address candidate.</param>
                [Theory]
                [InlineData("")]
                [InlineData("   ")]
                [InlineData("00:00:00:00:00:00")]
                [InlineData("AA:BB:CC:DD:EE")]
                public void Create_RejectsInvalidOrAllZeroMacAddress(string invalidMac)
                {
                        // Reject blank, truncated, or all-zero placeholder MAC addresses.
                        Assert.Throws<DomainValidationException>(() => HighscoreEntity.Create(
                                id: Guid.NewGuid(),
                                name: "Bob",
                                age: 12,
                                score: 10,
                                senderMacAddress: invalidMac,
                                gameId: Guid.NewGuid(),
                                playedAtUtc: DateTimeOffset.UtcNow));
                }

                /// <summary>
                /// Verifies zero or negative rankings and sync sequences are rejected.
                /// </summary>
                [Fact]
                public void AssignRankingAndSyncSequence_RejectNonPositiveValues()
                {
                        // Both ranking and sync sequence must be strictly positive (1-based).
                        HighscoreEntity entity = HighscoreEntity.Create(
                                id: Guid.NewGuid(),
                                name: "Bob",
                                age: null,
                                score: 50,
                                senderMacAddress: "aabbccddeeff",
                                gameId: Guid.NewGuid(),
                                playedAtUtc: DateTimeOffset.UtcNow);

                        Assert.Equal("AA:BB:CC:DD:EE:FF", entity.SenderMacAddress);
                        Assert.Throws<DomainValidationException>(() => entity.AssignRankingAtTimeOfScoring(0));
                        Assert.Throws<DomainValidationException>(() => entity.AssignSyncSequence(0));
                }

                /// <summary>
                /// Verifies repeated <see cref="HighscoreEntity.MarkDeleted"/> calls preserve the original deletion timestamp.
                /// </summary>
                [Fact]
                public void MarkDeleted_IsIdempotentAndPreservesOriginalTimestamp()
                {
                        // Calling MarkDeleted twice must retain the first UTC deletion timestamp.
                        HighscoreEntity entity = HighscoreEntity.Create(
                                id: Guid.NewGuid(),
                                name: "Bob",
                                age: 12,
                                score: 50,
                                senderMacAddress: "AA:BB:CC:DD:EE:FF",
                                gameId: Guid.NewGuid(),
                                playedAtUtc: DateTimeOffset.UtcNow);

                        DateTimeOffset firstDelete = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
                        DateTimeOffset secondDelete = firstDelete.AddHours(1);

                        entity.MarkDeleted(firstDelete);
                        entity.MarkDeleted(secondDelete);

                        Assert.True(entity.IsDeleted);
                        Assert.Equal(firstDelete, entity.DeletedAtUtc);
                }
        }
}

