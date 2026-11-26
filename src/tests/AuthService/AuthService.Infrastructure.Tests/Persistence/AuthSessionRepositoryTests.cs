
using AuthService.Application.Sessions;
using AuthService.Domain.Entities;
using AuthService.Domain.ValueObjects;
using AuthService.Infrastructure.Identity;
using AuthService.Infrastructure.Persistence;
using AuthService.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuthService.Infrastructure.Tests.Persistence;

public sealed class AuthSessionRepositoryTests
{
        private const string TEST_USERNAME = "RepositoryTestStaff";

        private static readonly DateTimeOffset START =
                new(
                        year: 2026,
                        month: 11,
                        day: 23,
                        hour: 9,
                        minute: 0,
                        second: 0,
                        offset: TimeSpan.Zero);

        private static RefreshTokenHash TestHash(char hexDigit)
        {
                return RefreshTokenHash.FromHex(
                        value: new string(c: hexDigit, count: 64));
        }

        private static async Task<(
                SqliteConnection Connection,
                DbContextOptions<AuthDbContext> Options,
                AuthSessionId SessionId)> CreateSeededDatabaseAsync()
        {
                SqliteConnection connection = new(
                        connectionString: "Data Source=:memory:");

                await connection.OpenAsync();

                // Keep the connection open so separate DbContexts share
                // the same real SQLite database during each test.
                DbContextOptions<AuthDbContext> options =
                        new DbContextOptionsBuilder<AuthDbContext>()
                                .UseSqlite(connection: connection)
                                .Options;

                await using AuthDbContext context = new(options: options);

                // Create the test schema without touching the development database.
                await context.Database.EnsureCreatedAsync();

                UserId userId = UserId.CreateNew();

                // Identity must exist before the session due to its foreign key.
                context.Users.Add(
                        entity: new AuthIdentityUser
                        {
                                Id = userId.Value,
                                UserName = TEST_USERNAME
                        });

                await context.SaveChangesAsync();

                AuthSession session = AuthSession.Create(
                        id: AuthSessionId.CreateNew(),
                        userId: userId,
                        nowUtc: START,
                        idleTimeout: TimeSpan.FromMinutes(value: 30),
                        absoluteTimeout: TimeSpan.FromHours(value: 8));

                session.RegisterInitialRefreshToken(
                        tokenHash: TestHash('A'),
                        nowUtc: START);

                AuthSessionRepository repository = new(
                        dbContext: context);

                await repository.AddAsync(session: session);

                return (connection, options, session.Id);
        }

        [Fact]
        public async Task RotationPersistsAcrossDbContextsAsync()
        {
                var setup = await CreateSeededDatabaseAsync();
                await using SqliteConnection connection = setup.Connection;

                RefreshTokenHash initial = TestHash('A');
                RefreshTokenHash replacement = TestHash('B');

                await using (AuthDbContext writer = new(options: setup.Options))
                {
                        AuthSessionRepository repository = new(
                                dbContext: writer);

                        VersionedAuthSession loaded =
                                Assert.IsType<VersionedAuthSession>(
                                        await repository.FindByRefreshTokenHashAsync(tokenHash: initial, cancellationToken: TestContext.Current.CancellationToken));

                        loaded.Session.RotateRefreshToken(
                                presentedHash: initial,
                                replacementHash: replacement,
                                nowUtc: START.AddMinutes(1));

                        await repository.UpdateAsync(session: loaded, cancellationToken: TestContext.Current.CancellationToken);
                }

                await using (AuthDbContext reader = new(options: setup.Options))
                {
                        AuthSessionRepository repository = new(
                                dbContext: reader);

                        // A consumed hash must still locate its session
                        // so later requests can detect token replay.
                        VersionedAuthSession restored =
                                Assert.IsType<VersionedAuthSession>(
                                        await repository.FindByRefreshTokenHashAsync(tokenHash: initial, cancellationToken: TestContext.Current.CancellationToken));

                        Assert.True(
                                condition: restored.Session
                                        .IsCurrentRefreshToken(replacement));

                        Assert.True(
                                condition: restored.Session
                                        .IsKnownConsumedRefreshToken(initial));
                }
        }

        [Fact]
        public async Task StaleVersionCannotOverwriteSessionAsync()
        {
                var setup = await CreateSeededDatabaseAsync();
                await using SqliteConnection connection = setup.Connection;

                await using AuthDbContext firstContext = new(
                        options: setup.Options);

                await using AuthDbContext secondContext = new(
                        options: setup.Options);

                AuthSessionRepository firstRepository = new(
                        dbContext: firstContext);

                AuthSessionRepository secondRepository = new(
                        dbContext: secondContext);

                // Both requests read the same version before either saves.
                VersionedAuthSession first =
                        Assert.IsType<VersionedAuthSession>(
                                await firstRepository.FindByIdAsync(sessionId: setup.SessionId, cancellationToken: TestContext.Current.CancellationToken));

                VersionedAuthSession second =
                        Assert.IsType<VersionedAuthSession>(
                                await secondRepository.FindByIdAsync(sessionId: setup.SessionId, cancellationToken: TestContext.Current.CancellationToken));

                Assert.Equal(
                        expected: first.Version,
                        actual: second.Version);

                first.Session.RecordUserActivity(
                        nowUtc: START.AddMinutes(5));

                second.Session.RecordUserActivity(
                        nowUtc: START.AddMinutes(10));

                await firstRepository.UpdateAsync(session: first, cancellationToken: TestContext.Current.CancellationToken);

                // Optimistic concurrency rejects the stale database version.
                await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
                        testCode: () => secondRepository.UpdateAsync(session: second, cancellationToken: TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task ConcurrentRotationCannotCommitTwiceAsync()
        {
                var setup = await CreateSeededDatabaseAsync();
                await using SqliteConnection connection = setup.Connection;

                await using AuthDbContext firstContext = new(
                        options: setup.Options);

                await using AuthDbContext secondContext = new(
                        options: setup.Options);

                AuthSessionRepository firstRepository = new(
                        dbContext: firstContext);

                AuthSessionRepository secondRepository = new(
                        dbContext: secondContext);

                RefreshTokenHash original = TestHash('A');
                RefreshTokenHash firstReplacement = TestHash('B');
                RefreshTokenHash secondReplacement = TestHash('C');

                VersionedAuthSession first =
                        Assert.IsType<VersionedAuthSession>(
                                await firstRepository.FindByRefreshTokenHashAsync(tokenHash: original, cancellationToken: TestContext.Current.CancellationToken));

                VersionedAuthSession second =
                        Assert.IsType<VersionedAuthSession>(
                                await secondRepository.FindByRefreshTokenHashAsync(tokenHash: original, cancellationToken: TestContext.Current.CancellationToken));

                first.Session.RotateRefreshToken(
                        presentedHash: original,
                        replacementHash: firstReplacement,
                        nowUtc: START.AddMinutes(1));

                second.Session.RotateRefreshToken(
                        presentedHash: original,
                        replacementHash: secondReplacement,
                        nowUtc: START.AddMinutes(1));

                await firstRepository.UpdateAsync(session: first, cancellationToken: TestContext.Current.CancellationToken);

                // A stale version or duplicate consumed hash must prevent
                // the second request from committing its competing rotation.
                await Assert.ThrowsAnyAsync<DbUpdateException>(
                        testCode: () => secondRepository.UpdateAsync(session: second, cancellationToken: TestContext.Current.CancellationToken));

                await using AuthDbContext verificationContext = new(
                        options: setup.Options);

                AuthSessionRepository verificationRepository = new(
                        dbContext: verificationContext);

                VersionedAuthSession persisted =
                        Assert.IsType<VersionedAuthSession>(
                                await verificationRepository.FindByIdAsync(sessionId: setup.SessionId, cancellationToken: TestContext.Current.CancellationToken));

                Assert.True(
                        condition: persisted.Session.IsCurrentRefreshToken(
                                tokenHash: firstReplacement));

                Assert.False(
                        condition: persisted.Session.IsCurrentRefreshToken(
                                tokenHash: secondReplacement));

                Assert.True(
                        condition: persisted.Session
                                .IsKnownConsumedRefreshToken(original));
        }
}
