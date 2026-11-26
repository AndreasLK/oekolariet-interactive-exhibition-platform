
using AuthService.Domain.Entities;
using AuthService.Domain.ValueObjects;
using AuthService.Infrastructure.Persistence;
using AuthService.Infrastructure.Persistence.Entities;
using Xunit;

namespace AuthService.Infrastructure.Tests.Persistence;

public sealed class AuthSessionRecordMapperTests
{
        private static readonly DateTimeOffset START =
                new(
                        year: 2026,
                        month: 11,
                        day: 23,
                        hour: 9,
                        minute: 0,
                        second: 0,
                        offset: TimeSpan.Zero);

        private static AuthSession CreateSession()
        {
                return AuthSession.Create(
                        id: AuthSessionId.CreateNew(),
                        userId: UserId.CreateNew(),
                        nowUtc: START,
                        idleTimeout: TimeSpan.FromMinutes(30),
                        absoluteTimeout: TimeSpan.FromHours(8));
        }

        private static RefreshTokenHash TestHash(char hexDigit)
        {
                return RefreshTokenHash.FromHex(
                        value: new string(c: hexDigit, count: 64));
        }

        [Fact]
        public void RoundTripPreservesRotationAndRevocation()
        {
                AuthSession original = CreateSession();

                RefreshTokenHash first = TestHash('A');
                RefreshTokenHash second = TestHash('B');

                original.RegisterInitialRefreshToken(
                        tokenHash: first,
                        nowUtc: START);

                original.RotateRefreshToken(
                        presentedHash: first,
                        replacementHash: second,
                        nowUtc: START.AddMinutes(1));

                original.RecordUserActivity(
                        nowUtc: START.AddMinutes(10));

                original.RevokeForRefreshTokenReplay(
                        presentedHash: first,
                        nowUtc: START.AddMinutes(12));

                // Mapping must preserve security state across persistence.
                AuthSessionRecord record =
                        AuthSessionRecordMapper.ToNewRecord(
                                session: original);

                AuthSession restored =
                        AuthSessionRecordMapper.ToDomain(
                                record: record);

                Assert.Equal(expected: original.Id, actual: restored.Id);
                Assert.Equal(expected: original.UserId, actual: restored.UserId);

                Assert.Equal(
                        expected: original.AbsoluteExpiresAtUtc,
                        actual: restored.AbsoluteExpiresAtUtc);

                Assert.True(
                        condition: restored.IsCurrentRefreshToken(second));

                Assert.True(
                        condition: restored.IsKnownConsumedRefreshToken(first));

                Assert.Equal(
                        expected: original.SuspectedReplayDetectedAtUtc,
                        actual: restored.SuspectedReplayDetectedAtUtc);

                Assert.False(
                        condition: restored.IsActiveAt(
                                nowUtc: START.AddMinutes(13)));

                Assert.NotEqual(
                        expected: Guid.Empty,
                        actual: record.Version);
        }

        [Fact]
        public void DatabaseTimestampsAreRestoredAsUtc()
        {
                AuthSession original = CreateSession();

                AuthSessionRecord record =
                        AuthSessionRecordMapper.ToNewRecord(
                                session: original);

                // Simulate SQLite returning a DateTime without Kind.
                record.CreatedAtUtc = DateTime.SpecifyKind(
                        value: record.CreatedAtUtc,
                        kind: DateTimeKind.Unspecified);

                AuthSession restored =
                        AuthSessionRecordMapper.ToDomain(
                                record: record);

                Assert.Equal(
                        expected: original.CreatedAtUtc,
                        actual: restored.CreatedAtUtc);

                Assert.Equal(
                        expected: TimeSpan.Zero,
                        actual: restored.CreatedAtUtc.Offset);
        }

        [Fact]
        public void ConflictingPersistedHashesAreRejected()
        {
                AuthSession session = CreateSession();
                RefreshTokenHash hash = TestHash('A');

                session.RegisterInitialRefreshToken(
                        tokenHash: hash,
                        nowUtc: START);

                AuthSessionRecord record =
                        AuthSessionRecordMapper.ToNewRecord(
                                session: session);

                // A token cannot be both current and previously consumed.
                record.ConsumedRefreshTokens.Add(
                        item: new ConsumedRefreshTokenRecord
                        {
                                AuthSessionId = record.Id,
                                TokenHash = hash.Value
                        });

                Assert.Throws<ArgumentException>(
                        testCode: () => AuthSessionRecordMapper.ToDomain(
                                record: record));
        }
}
