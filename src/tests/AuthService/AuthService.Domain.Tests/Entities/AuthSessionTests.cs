
using AuthService.Domain.Entities;
using AuthService.Domain.ValueObjects;
using Xunit;

namespace AuthService.Domain.Tests.Entities;

public sealed class AuthSessionTests
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
                        idleTimeout: TimeSpan.FromMinutes(value: 30),
                        absoluteTimeout: TimeSpan.FromHours(value: 8));
        }

        [Fact]
        public void NewSessionIsActive()
        {
                AuthSession session = CreateSession();

                Assert.True(
                        condition: session.IsActiveAt(nowUtc: START));
        }

        [Fact]
        public void SessionExpiresAfterThirtyMinutesOfInactivity()
        {
                AuthSession session = CreateSession();

                Assert.True(
                        condition: session.IsActiveAt(
                                nowUtc: START.AddMinutes(29)));

                Assert.False(
                        condition: session.IsActiveAt(
                                nowUtc: START.AddMinutes(30)));
        }


        [Fact]
        public void UserActivityExtendsIdleButNotAbsoluteLifetime()
        {
                AuthSession session = CreateSession();

                // Activity every 20 minutes prevents idle expiration.
                for (int minute = 20; minute < 480; minute += 20)
                {
                        session.RecordUserActivity(
                                nowUtc: START.AddMinutes(minute));
                }

                // The session is still active just before absolute expiration.
                Assert.True(
                        condition: session.IsActiveAt(
                                nowUtc: START.AddMinutes(479)));

                // Absolute expiration cannot be extended by user activity.
                Assert.False(
                        condition: session.IsActiveAt(
                                nowUtc: START.AddHours(8)));
        }


        [Fact]
        public void RevokedSessionCannotBeReactivated()
        {
                AuthSession session = CreateSession();

                session.Revoke(nowUtc: START.AddMinutes(5));

                Assert.False(
                        condition: session.IsActiveAt(
                                nowUtc: START.AddMinutes(6)));

                Assert.Throws<InvalidOperationException>(
                        testCode: () => session.RecordUserActivity(
                                nowUtc: START.AddMinutes(7)));
        }


        private static RefreshTokenHash TestHash(char hexDigit)
        {
                return RefreshTokenHash.FromHex(
                        value: new string(c: hexDigit, count: 64));
        }

        [Fact]
        public void RotationConsumesPreviousRefreshToken()
        {
                AuthSession session = CreateSession();

                RefreshTokenHash first = TestHash('A');
                RefreshTokenHash second = TestHash('B');

                session.RegisterInitialRefreshToken(
                        tokenHash: first,
                        nowUtc: START);

                session.RotateRefreshToken(
                        presentedHash: first,
                        replacementHash: second,
                        nowUtc: START.AddMinutes(1));

                Assert.False(
                        condition: session.IsCurrentRefreshToken(first));

                Assert.True(
                        condition: session.IsKnownConsumedRefreshToken(first));

                Assert.True(
                        condition: session.IsCurrentRefreshToken(second));
        }

        [Fact]
        public void ConsumedRefreshTokenCannotBeRotatedAgain()
        {
                AuthSession session = CreateSession();

                RefreshTokenHash first = TestHash('A');
                RefreshTokenHash second = TestHash('B');
                RefreshTokenHash third = TestHash('C');

                session.RegisterInitialRefreshToken(
                        tokenHash: first,
                        nowUtc: START);

                session.RotateRefreshToken(
                        presentedHash: first,
                        replacementHash: second,
                        nowUtc: START.AddMinutes(1));

                Assert.Throws<InvalidOperationException>(
                        testCode: () => session.RotateRefreshToken(
                                presentedHash: first,
                                replacementHash: third,
                                nowUtc: START.AddMinutes(2)));

                Assert.True(
                        condition: session.IsCurrentRefreshToken(second));
        }

        [Fact]
        public void UnknownRefreshTokenDoesNotRevokeSession()
        {
                AuthSession session = CreateSession();

                RefreshTokenHash current = TestHash('A');
                RefreshTokenHash unknown = TestHash('B');

                session.RegisterInitialRefreshToken(
                        tokenHash: current,
                        nowUtc: START);

                Assert.False(
                        condition: session.IsCurrentRefreshToken(unknown));

                Assert.False(
                        condition: session.IsKnownConsumedRefreshToken(unknown));

                Assert.Null(session.RevokedAtUtc);
                Assert.True(
                        condition: session.IsActiveAt(
                                nowUtc: START.AddMinutes(1)));
        }

        [Fact]
        public void ConsumedTokenReplayRevokesSession()
        {
                AuthSession session = CreateSession();

                RefreshTokenHash first = TestHash('A');
                RefreshTokenHash second = TestHash('B');

                session.RegisterInitialRefreshToken(
                        tokenHash: first,
                        nowUtc: START);

                session.RotateRefreshToken(
                        presentedHash: first,
                        replacementHash: second,
                        nowUtc: START.AddMinutes(1));

                session.RevokeForRefreshTokenReplay(
                        presentedHash: first,
                        nowUtc: START.AddMinutes(2));

                Assert.False(
                        condition: session.IsActiveAt(
                                nowUtc: START.AddMinutes(3)));

                Assert.Equal(
                        expected: START.AddMinutes(2),
                        actual: session.SuspectedReplayDetectedAtUtc);
        }

        [Fact]
        public void UnknownTokenCannotTriggerReplayRevocation()
        {
                AuthSession session = CreateSession();

                session.RegisterInitialRefreshToken(
                        tokenHash: TestHash('A'),
                        nowUtc: START);

                Assert.Throws<InvalidOperationException>(
                        testCode: () => session.RevokeForRefreshTokenReplay(
                                presentedHash: TestHash('B'),
                                nowUtc: START.AddMinutes(1)));

                Assert.Null(session.RevokedAtUtc);
                Assert.Null(session.SuspectedReplayDetectedAtUtc);
        }

        [Fact]
        public void ReplayRevocationDoesNotAffectAnotherSession()
        {
                UserId sharedUserId = UserId.CreateNew();

                AuthSession firstSession = AuthSession.Create(
                        id: AuthSessionId.CreateNew(),
                        userId: sharedUserId,
                        nowUtc: START,
                        idleTimeout: TimeSpan.FromMinutes(30),
                        absoluteTimeout: TimeSpan.FromHours(8));

                AuthSession secondSession = AuthSession.Create(
                        id: AuthSessionId.CreateNew(),
                        userId: sharedUserId,
                        nowUtc: START,
                        idleTimeout: TimeSpan.FromMinutes(30),
                        absoluteTimeout: TimeSpan.FromHours(8));

                RefreshTokenHash oldHash = TestHash('A');

                firstSession.RegisterInitialRefreshToken(
                        tokenHash: oldHash,
                        nowUtc: START);

                firstSession.RotateRefreshToken(
                        presentedHash: oldHash,
                        replacementHash: TestHash('B'),
                        nowUtc: START.AddMinutes(1));

                firstSession.RevokeForRefreshTokenReplay(
                        presentedHash: oldHash,
                        nowUtc: START.AddMinutes(2));

                Assert.False(
                        condition: firstSession.IsActiveAt(
                                nowUtc: START.AddMinutes(3)));

                Assert.True(
                        condition: secondSession.IsActiveAt(
                                nowUtc: START.AddMinutes(3)));
        }

        [Fact]
        public void RestorePreservesRefreshTokenRotationState()
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

                AuthSession restored = AuthSession.Restore(
                        id: original.Id,
                        userId: original.UserId,
                        createdAtUtc: original.CreatedAtUtc,
                        lastUserActivityAtUtc: original.LastUserActivityAtUtc,
                        absoluteExpiresAtUtc: original.AbsoluteExpiresAtUtc,
                        idleTimeout: original.IdleTimeout,
                        revokedAtUtc: original.RevokedAtUtc,
                        suspectedReplayDetectedAtUtc:
                                original.SuspectedReplayDetectedAtUtc,
                        currentRefreshTokenHash: original.CurrentRefreshTokenHash,
                        consumedRefreshTokenHashes:
                                original.ConsumedRefreshTokenHashes);

                Assert.Equal(expected: original.Id, actual: restored.Id);

                Assert.True(
                        condition: restored.IsCurrentRefreshToken(second));

                Assert.True(
                        condition: restored.IsKnownConsumedRefreshToken(first));

                Assert.True(
                        condition: restored.IsActiveAt(
                                nowUtc: START.AddMinutes(39)));

                Assert.Equal(
                        expected: original.AbsoluteExpiresAtUtc,
                        actual: restored.AbsoluteExpiresAtUtc);
        }

        [Fact]
        public void RestorePreservesReplayRevocation()
        {
                AuthSession original = CreateSession();

                RefreshTokenHash first = TestHash('A');

                original.RegisterInitialRefreshToken(
                        tokenHash: first,
                        nowUtc: START);

                original.RotateRefreshToken(
                        presentedHash: first,
                        replacementHash: TestHash('B'),
                        nowUtc: START.AddMinutes(1));

                original.RevokeForRefreshTokenReplay(
                        presentedHash: first,
                        nowUtc: START.AddMinutes(2));

                AuthSession restored = AuthSession.Restore(
                        id: original.Id,
                        userId: original.UserId,
                        createdAtUtc: original.CreatedAtUtc,
                        lastUserActivityAtUtc: original.LastUserActivityAtUtc,
                        absoluteExpiresAtUtc: original.AbsoluteExpiresAtUtc,
                        idleTimeout: original.IdleTimeout,
                        revokedAtUtc: original.RevokedAtUtc,
                        suspectedReplayDetectedAtUtc:
                                original.SuspectedReplayDetectedAtUtc,
                        currentRefreshTokenHash: original.CurrentRefreshTokenHash,
                        consumedRefreshTokenHashes:
                                original.ConsumedRefreshTokenHashes);

                Assert.False(
                        condition: restored.IsActiveAt(
                                nowUtc: START.AddMinutes(3)));

                Assert.Equal(
                        expected: original.SuspectedReplayDetectedAtUtc,
                        actual: restored.SuspectedReplayDetectedAtUtc);
        }

        [Fact]
        public void RestoreRejectsConflictingRefreshTokenState()
        {
                RefreshTokenHash hash = TestHash('A');

                Assert.Throws<ArgumentException>(
                        testCode: () => AuthSession.Restore(
                                id: AuthSessionId.CreateNew(),
                                userId: UserId.CreateNew(),
                                createdAtUtc: START,
                                lastUserActivityAtUtc: START,
                                absoluteExpiresAtUtc: START.AddHours(8),
                                idleTimeout: TimeSpan.FromMinutes(30),
                                revokedAtUtc: null,
                                suspectedReplayDetectedAtUtc: null,
                                currentRefreshTokenHash: hash,
                                consumedRefreshTokenHashes: [hash]));
        }

}
