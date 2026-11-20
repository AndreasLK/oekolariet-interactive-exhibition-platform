using EditorService.Domain.Enums;
using EditorService.Domain.Exceptions;
using EditorService.Tests.TestDoubles;

namespace EditorService.Tests.Domain;

/// <summary>
/// Tests the shared rules in GameEntity: creating a game, the edit lock and publishing.
/// </summary>
public class GameEntityTests
{
        // A fixed point in time, so the tests give the same result every time they run.
        private static readonly DateTimeOffset NOW = new(year: 2026, month: 11, day: 20, hour: 10, minute: 0, second: 0, offset: TimeSpan.Zero);

        private const string ACTOR = "test-user";

        // ---------- Creating a game ----------

        /// <summary>A new game gets the title and starts as a draft.</summary>
        [Fact]
        public void CreateSetsTitleAndDraftStatus()
        {
                // Act
                TestGame game = TestGame.Create(title: "Affaldsspil", now: NOW);

                // Assert
                Assert.Equal(expected: "Affaldsspil", actual: game.Title);
                Assert.Equal(expected: GameStatus.Draft, actual: game.Status);
                Assert.Null(@object: game.PublishedAt);
        }

        /// <summary>Spaces before and after the title are removed.</summary>
        [Fact]
        public void CreateTrimsTitle()
        {
                TestGame game = TestGame.Create(title: "  Affaldsspil  ", now: NOW);

                Assert.Equal(expected: "Affaldsspil", actual: game.Title);
        }

        /// <summary>An empty title is not allowed.</summary>
        /// <param name="title">An empty or whitespace title.</param>
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void CreateThrowsWhenTitleIsEmpty(string title)
        {
                Assert.Throws<DomainValidationException>(testCode: () => TestGame.Create(title: title, now: NOW));
        }

        /// <summary>A title longer than 150 characters is not allowed.</summary>
        [Fact]
        public void CreateThrowsWhenTitleIsTooLong()
        {
                string longTitle = new(c: 'a', count: 151);

                Assert.Throws<DomainValidationException>(testCode: () => TestGame.Create(title: longTitle, now: NOW));
        }

        // ---------- Taking the lock ----------

        /// <summary>When nobody has the lock, you get it.</summary>
        [Fact]
        public void AcquireLockGivesLockWhenGameIsUnlocked()
        {
                // Arrange
                TestGame game = TestGame.Create(title: "Affaldsspil", now: NOW);

                // Act
                Guid token = game.AcquireLock(holderName: "Lise", deviceName: "PC1", now: NOW);

                // Assert
                Assert.NotEqual(expected: Guid.Empty, actual: token);
                Assert.True(condition: game.IsLocked(now: NOW));
                Assert.Equal(expected: "Lise", actual: game.EditLock!.HolderName);
                Assert.Equal(expected: "PC1", actual: game.EditLock.DeviceName);
        }

        /// <summary>When someone else has the lock, you get an error that says who.</summary>
        [Fact]
        public void AcquireLockThrowsWhenSomeoneElseHoldsTheLock()
        {
                TestGame game = TestGame.Create(title: "Affaldsspil", now: NOW);
                game.AcquireLock(holderName: "Lise", deviceName: "PC1", now: NOW);

                GameLockedException exception = Assert.Throws<GameLockedException>(
                        testCode: () => game.AcquireLock(holderName: "Ole", deviceName: "PC2", now: NOW.AddMinutes(minutes: 5)));

                Assert.Contains(expectedSubstring: "Lise", actualString: exception.Message);
                Assert.Contains(expectedSubstring: "PC1", actualString: exception.Message);
        }

        /// <summary>The same person on the same device gets the same lock back.</summary>
        [Fact]
        public void AcquireLockRenewsWhenSameHolderAndDevice()
        {
                TestGame game = TestGame.Create(title: "Affaldsspil", now: NOW);
                Guid firstToken = game.AcquireLock(holderName: "Lise", deviceName: "PC1", now: NOW);

                Guid secondToken = game.AcquireLock(holderName: "lise", deviceName: "pc1", now: NOW.AddMinutes(minutes: 10));

                Assert.Equal(expected: firstToken, actual: secondToken);
                Assert.Equal(expected: NOW.AddMinutes(minutes: 25), actual: game.EditLock!.ExpiresAt);
        }

        /// <summary>After 15 minutes the lock has expired, and someone else can take it.</summary>
        [Fact]
        public void AcquireLockSucceedsWhenOldLockHasExpired()
        {
                TestGame game = TestGame.Create(title: "Affaldsspil", now: NOW);
                Guid oldToken = game.AcquireLock(holderName: "Lise", deviceName: "PC1", now: NOW);

                Guid newToken = game.AcquireLock(holderName: "Ole", deviceName: "PC2", now: NOW.AddMinutes(minutes: 16));

                Assert.NotEqual(expected: oldToken, actual: newToken);
                Assert.Equal(expected: "Ole", actual: game.EditLock!.HolderName);
        }

        /// <summary>Releasing the lock removes it.</summary>
        [Fact]
        public void ReleaseLockRemovesLock()
        {
                TestGame game = TestGame.Create(title: "Affaldsspil", now: NOW);
                Guid token = game.AcquireLock(holderName: "Lise", deviceName: "PC1", now: NOW);

                game.ReleaseLock(lockToken: token);

                Assert.False(condition: game.IsLocked(now: NOW));
        }

        // ---------- Changing the game ----------

        /// <summary>You cannot change the game without taking the lock first.</summary>
        [Fact]
        public void RenameThrowsWithoutLock()
        {
                TestGame game = TestGame.Create(title: "Affaldsspil", now: NOW);

                Assert.Throws<EditLockRequiredException>(
                        testCode: () => game.Rename(title: "Nyt navn", lockToken: Guid.NewGuid(), actorId: ACTOR, now: NOW));
        }

        /// <summary>You cannot change the game with someone else's token.</summary>
        [Fact]
        public void RenameThrowsWithWrongToken()
        {
                TestGame game = TestGame.Create(title: "Affaldsspil", now: NOW);
                game.AcquireLock(holderName: "Lise", deviceName: "PC1", now: NOW);

                Assert.Throws<EditLockRequiredException>(
                        testCode: () => game.Rename(title: "Nyt navn", lockToken: Guid.NewGuid(), actorId: ACTOR, now: NOW));
        }

        /// <summary>You cannot change the game when your lock has expired.</summary>
        [Fact]
        public void RenameThrowsWhenLockHasExpired()
        {
                TestGame game = TestGame.Create(title: "Affaldsspil", now: NOW);
                Guid token = game.AcquireLock(holderName: "Lise", deviceName: "PC1", now: NOW);

                Assert.Throws<EditLockRequiredException>(
                        testCode: () => game.Rename(title: "Nyt navn", lockToken: token, actorId: ACTOR, now: NOW.AddMinutes(minutes: 16)));
        }

        /// <summary>With a valid lock, the title is changed.</summary>
        [Fact]
        public void RenameChangesTitleWithValidLock()
        {
                TestGame game = TestGame.Create(title: "Affaldsspil", now: NOW);
                Guid token = game.AcquireLock(holderName: "Lise", deviceName: "PC1", now: NOW);

                game.Rename(title: "Sortér affaldet", lockToken: token, actorId: ACTOR, now: NOW);

                Assert.Equal(expected: "Sortér affaldet", actual: game.Title);
        }

        // ---------- Publishing ----------

        /// <summary>A game without a template cannot be published.</summary>
        [Fact]
        public void PublishThrowsWithoutTemplate()
        {
                TestGame game = TestGame.Create(title: "Affaldsspil", now: NOW);
                Guid token = game.AcquireLock(holderName: "Lise", deviceName: "PC1", now: NOW);

                Assert.Throws<DomainValidationException>(
                        testCode: () => game.Publish(lockToken: token, actorId: ACTOR, now: NOW));
        }

        /// <summary>A complete game is published and gets a publish time.</summary>
        [Fact]
        public void PublishSetsStatusAndTime()
        {
                TestGame game = TestGame.Create(title: "Affaldsspil", now: NOW);
                Guid token = game.AcquireLock(holderName: "Lise", deviceName: "PC1", now: NOW);
                game.ChangeTemplate(templateId: Guid.NewGuid(), lockToken: token, actorId: ACTOR, now: NOW);

                game.Publish(lockToken: token, actorId: ACTOR, now: NOW);

                Assert.Equal(expected: GameStatus.Published, actual: game.Status);
                Assert.Equal(expected: NOW, actual: game.PublishedAt);
        }

        /// <summary>Changing a published game turns it back into a draft, but it keeps its publish time.</summary>
        [Fact]
        public void ChangingPublishedGameMakesItDraftAgain()
        {
                TestGame game = TestGame.Create(title: "Affaldsspil", now: NOW);
                Guid token = game.AcquireLock(holderName: "Lise", deviceName: "PC1", now: NOW);
                game.ChangeTemplate(templateId: Guid.NewGuid(), lockToken: token, actorId: ACTOR, now: NOW);
                game.Publish(lockToken: token, actorId: ACTOR, now: NOW);

                game.Rename(title: "Ny titel", lockToken: token, actorId: ACTOR, now: NOW.AddMinutes(minutes: 1));

                Assert.Equal(expected: GameStatus.Draft, actual: game.Status);
                Assert.NotNull(@object: game.PublishedAt);
        }
}
