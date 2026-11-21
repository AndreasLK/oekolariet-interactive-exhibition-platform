using EditorService.Domain.Entities.SortingGames;
using EditorService.Domain.Enums;
using EditorService.Domain.Exceptions;

namespace EditorService.Tests.Domain
{
        /// <summary>
        /// Tests the rules of the sorting game: category receivers, category items and publishing.
        /// </summary>
        public class SortingGameEntityTests
        {
                // A fixed point in time, so the tests give the same result every time they run.
                private static readonly DateTimeOffset NOW = new(year: 2026, month: 11, day: 21, hour: 10, minute: 0, second: 0, offset: TimeSpan.Zero);

                private const string ACTOR = "test-user";
                private const string COLOR = "#1D9E75";

                // ---------- Helpers ----------

                /// <summary>Creates a sorting game and takes the lock, so the test can change it.</summary>
                /// <param name="lockToken">The token of the lock that was taken.</param>
                /// <returns>A new sorting game with an active lock.</returns>
                private static SortingGameEntity CreateLockedGame(out Guid lockToken)
                {
                        SortingGameEntity game = SortingGameEntity.Create(title: "Affaldsspil", actorId: ACTOR, now: NOW);
                        lockToken = game.AcquireLock(holderName: "Lise", deviceName: "PC1", now: NOW);
                        return game;
                }

                // ---------- Creating a sorting game ----------

                /// <summary>A new sorting game has the right type, is a draft and is empty.</summary>
                [Fact]
                public void CreateMakesEmptyDraftSortingGame()
                {
                        // Arrange
                        string title = "Affaldsspil";

                        // Act
                        SortingGameEntity game = SortingGameEntity.Create(title: title, actorId: ACTOR, now: NOW);

                        // Assert
                        Assert.Equal(expected: GameType.SortingGame, actual: game.Type);
                        Assert.Equal(expected: GameStatus.Draft, actual: game.Status);
                        Assert.Empty(collection: game.CategoryReceivers);
                        Assert.Empty(collection: game.CategoryItems);
                }

                // ---------- Adding a category receiver ----------

                /// <summary>A category receiver with a name and a color is added to the game.</summary>
                [Fact]
                public void AddCategoryReceiverAddsReceiver()
                {
                        // Arrange
                        SortingGameEntity game = CreateLockedGame(lockToken: out Guid token);

                        // Act
                        CategoryReceiverEntity receiver = game.AddCategoryReceiver(name: "Plast", color: COLOR, imageId: null, lockToken: token, actorId: ACTOR, now: NOW);

                        // Assert
                        Assert.Single(collection: game.CategoryReceivers);
                        Assert.Equal(expected: "Plast", actual: receiver.Name);
                        Assert.Equal(expected: COLOR, actual: receiver.Color);
                }

                /// <summary>A category receiver may use an image instead of a color.</summary>
                [Fact]
                public void AddCategoryReceiverAcceptsImageWithoutColor()
                {
                        // Arrange
                        SortingGameEntity game = CreateLockedGame(lockToken: out Guid token);
                        Guid imageId = Guid.NewGuid();

                        // Act
                        CategoryReceiverEntity receiver = game.AddCategoryReceiver(name: "Plast", color: null, imageId: imageId, lockToken: token, actorId: ACTOR, now: NOW);

                        // Assert
                        Assert.Null(@object: receiver.Color);
                        Assert.Equal(expected: imageId, actual: receiver.ImageId);
                }

                /// <summary>A category receiver must have a color or an image, so the player can see it.</summary>
                [Fact]
                public void AddCategoryReceiverThrowsWithoutColorAndImage()
                {
                        // Arrange
                        SortingGameEntity game = CreateLockedGame(lockToken: out Guid token);

                        // Act
                        Action act = () => game.AddCategoryReceiver(name: "Plast", color: null, imageId: null, lockToken: token, actorId: ACTOR, now: NOW);

                        // Assert
                        Assert.Throws<DomainValidationException>(testCode: act);
                }

                /// <summary>A category receiver must have a name.</summary>
                [Fact]
                public void AddCategoryReceiverThrowsWithEmptyName()
                {
                        // Arrange
                        SortingGameEntity game = CreateLockedGame(lockToken: out Guid token);

                        // Act
                        Action act = () => game.AddCategoryReceiver(name: "  ", color: COLOR, imageId: null, lockToken: token, actorId: ACTOR, now: NOW);

                        // Assert
                        Assert.Throws<DomainValidationException>(testCode: act);
                }

                /// <summary>Nothing can be added without the lock.</summary>
                [Fact]
                public void AddCategoryReceiverThrowsWithoutLock()
                {
                        // Arrange: a new game where nobody has taken the lock
                        SortingGameEntity game = SortingGameEntity.Create(title: "Affaldsspil", actorId: ACTOR, now: NOW);

                        // Act
                        Action act = () => game.AddCategoryReceiver(name: "Plast", color: COLOR, imageId: null, lockToken: Guid.NewGuid(), actorId: ACTOR, now: NOW);

                        // Assert
                        Assert.Throws<EditLockRequiredException>(testCode: act);
                }

                // ---------- Adding a category item ----------

                /// <summary>A category item is added and points to its category receiver.</summary>
                [Fact]
                public void AddCategoryItemAddsItemToReceiver()
                {
                        // Arrange
                        SortingGameEntity game = CreateLockedGame(lockToken: out Guid token);
                        CategoryReceiverEntity receiver = game.AddCategoryReceiver(name: "Pap", color: COLOR, imageId: null, lockToken: token, actorId: ACTOR, now: NOW);

                        // Act
                        CategoryItemEntity item = game.AddCategoryItem(name: "Mælkekarton", explanation: "Skyl den og læg den i pap.", imageId: Guid.NewGuid(), categoryReceiverId: receiver.Id, lockToken: token, actorId: ACTOR, now: NOW);

                        // Assert
                        Assert.Single(collection: game.CategoryItems);
                        Assert.Equal(expected: receiver.Id, actual: item.CategoryReceiverId);
                }

                /// <summary>A category item cannot point to a category receiver that is not in the game.</summary>
                [Fact]
                public void AddCategoryItemThrowsWhenReceiverDoesNotExist()
                {
                        // Arrange
                        SortingGameEntity game = CreateLockedGame(lockToken: out Guid token);
                        Guid unknownReceiverId = Guid.NewGuid();

                        // Act
                        Action act = () => game.AddCategoryItem(name: "Mælkekarton", explanation: "Pap.", imageId: Guid.NewGuid(), categoryReceiverId: unknownReceiverId, lockToken: token, actorId: ACTOR, now: NOW);

                        // Assert
                        Assert.Throws<DomainValidationException>(testCode: act);
                }

                /// <summary>A category item must have an explanation for the player (F17).</summary>
                [Fact]
                public void AddCategoryItemThrowsWithoutExplanation()
                {
                        // Arrange
                        SortingGameEntity game = CreateLockedGame(lockToken: out Guid token);
                        CategoryReceiverEntity receiver = game.AddCategoryReceiver(name: "Pap", color: COLOR, imageId: null, lockToken: token, actorId: ACTOR, now: NOW);

                        // Act
                        Action act = () => game.AddCategoryItem(name: "Mælkekarton", explanation: "", imageId: Guid.NewGuid(), categoryReceiverId: receiver.Id, lockToken: token, actorId: ACTOR, now: NOW);

                        // Assert
                        Assert.Throws<DomainValidationException>(testCode: act);
                }

                // ---------- Removing ----------

                /// <summary>Removing a category receiver also removes its items, but not the items of other receivers.</summary>
                [Fact]
                public void RemoveCategoryReceiverRemovesItsItems()
                {
                        // Arrange: two receivers with one item each
                        SortingGameEntity game = CreateLockedGame(lockToken: out Guid token);
                        CategoryReceiverEntity plast = game.AddCategoryReceiver(name: "Plast", color: COLOR, imageId: null, lockToken: token, actorId: ACTOR, now: NOW);
                        CategoryReceiverEntity pap = game.AddCategoryReceiver(name: "Pap", color: COLOR, imageId: null, lockToken: token, actorId: ACTOR, now: NOW);
                        game.AddCategoryItem(name: "Flaske", explanation: "Plast.", imageId: Guid.NewGuid(), categoryReceiverId: plast.Id, lockToken: token, actorId: ACTOR, now: NOW);
                        CategoryItemEntity karton = game.AddCategoryItem(name: "Karton", explanation: "Pap.", imageId: Guid.NewGuid(), categoryReceiverId: pap.Id, lockToken: token, actorId: ACTOR, now: NOW);

                        // Act: remove "Plast"
                        game.RemoveCategoryReceiver(categoryReceiverId: plast.Id, lockToken: token, actorId: ACTOR, now: NOW);

                        // Assert: only "Pap" and its item are left
                        CategoryReceiverEntity remainingReceiver = Assert.Single(collection: game.CategoryReceivers);
                        CategoryItemEntity remainingItem = Assert.Single(collection: game.CategoryItems);
                        Assert.Equal(expected: pap.Id, actual: remainingReceiver.Id);
                        Assert.Equal(expected: karton.Id, actual: remainingItem.Id);
                }

                /// <summary>Removing a category receiver that does not exist gives an error.</summary>
                [Fact]
                public void RemoveCategoryReceiverThrowsWhenItDoesNotExist()
                {
                        // Arrange
                        SortingGameEntity game = CreateLockedGame(lockToken: out Guid token);
                        Guid unknownReceiverId = Guid.NewGuid();

                        // Act
                        Action act = () => game.RemoveCategoryReceiver(categoryReceiverId: unknownReceiverId, lockToken: token, actorId: ACTOR, now: NOW);

                        // Assert
                        Assert.Throws<DomainValidationException>(testCode: act);
                }

                /// <summary>Removing a category item removes only that item.</summary>
                [Fact]
                public void RemoveCategoryItemRemovesItem()
                {
                        // Arrange
                        SortingGameEntity game = CreateLockedGame(lockToken: out Guid token);
                        CategoryReceiverEntity receiver = game.AddCategoryReceiver(name: "Pap", color: COLOR, imageId: null, lockToken: token, actorId: ACTOR, now: NOW);
                        CategoryItemEntity item = game.AddCategoryItem(name: "Karton", explanation: "Pap.", imageId: Guid.NewGuid(), categoryReceiverId: receiver.Id, lockToken: token, actorId: ACTOR, now: NOW);

                        // Act
                        game.RemoveCategoryItem(categoryItemId: item.Id, lockToken: token, actorId: ACTOR, now: NOW);

                        // Assert
                        Assert.Empty(collection: game.CategoryItems);
                        Assert.Single(collection: game.CategoryReceivers);
                }

                // ---------- Updating ----------

                /// <summary>A category receiver can get a new name and appearance.</summary>
                [Fact]
                public void UpdateCategoryReceiverChangesNameAndAppearance()
                {
                        // Arrange
                        SortingGameEntity game = CreateLockedGame(lockToken: out Guid token);
                        CategoryReceiverEntity receiver = game.AddCategoryReceiver(name: "Plast", color: COLOR, imageId: null, lockToken: token, actorId: ACTOR, now: NOW);
                        Guid imageId = Guid.NewGuid();

                        // Act
                        game.UpdateCategoryReceiver(categoryReceiverId: receiver.Id, name: "Hård plast", color: null, imageId: imageId, lockToken: token, actorId: ACTOR, now: NOW);

                        // Assert
                        Assert.Equal(expected: "Hård plast", actual: receiver.Name);
                        Assert.Null(@object: receiver.Color);
                        Assert.Equal(expected: imageId, actual: receiver.ImageId);
                }

                /// <summary>A category item can get a new name and explanation, but stays in its receiver.</summary>
                [Fact]
                public void UpdateCategoryItemChangesTextButKeepsReceiver()
                {
                        // Arrange
                        SortingGameEntity game = CreateLockedGame(lockToken: out Guid token);
                        CategoryReceiverEntity receiver = game.AddCategoryReceiver(name: "Pap", color: COLOR, imageId: null, lockToken: token, actorId: ACTOR, now: NOW);
                        CategoryItemEntity item = game.AddCategoryItem(name: "Karton", explanation: "Pap.", imageId: Guid.NewGuid(), categoryReceiverId: receiver.Id, lockToken: token, actorId: ACTOR, now: NOW);

                        // Act
                        game.UpdateCategoryItem(categoryItemId: item.Id, name: "Mælkekarton", explanation: "Skyl den først.", imageId: item.ImageId, categoryReceiverId: receiver.Id, lockToken: token, actorId: ACTOR, now: NOW);

                        // Assert
                        Assert.Equal(expected: "Mælkekarton", actual: item.Name);
                        Assert.Equal(expected: "Skyl den først.", actual: item.Explanation);
                        Assert.Equal(expected: receiver.Id, actual: item.CategoryReceiverId);
                }

                // ---------- Publishing ----------

                /// <summary>A sorting game without category receivers cannot be published.</summary>
                [Fact]
                public void PublishThrowsWithoutCategoryReceivers()
                {
                        // Arrange
                        SortingGameEntity game = CreateLockedGame(lockToken: out Guid token);
                        game.ChangeTemplate(templateId: Guid.NewGuid(), lockToken: token, actorId: ACTOR, now: NOW);

                        // Act
                        Action act = () => game.Publish(lockToken: token, actorId: ACTOR, now: NOW);

                        // Assert
                        Assert.Throws<DomainValidationException>(testCode: act);
                }

                /// <summary>A sorting game with a category receiver but no items cannot be published.</summary>
                [Fact]
                public void PublishThrowsWithoutCategoryItems()
                {
                        // Arrange
                        SortingGameEntity game = CreateLockedGame(lockToken: out Guid token);
                        game.ChangeTemplate(templateId: Guid.NewGuid(), lockToken: token, actorId: ACTOR, now: NOW);
                        game.AddCategoryReceiver(name: "Pap", color: COLOR, imageId: null, lockToken: token, actorId: ACTOR, now: NOW);

                        // Act
                        Action act = () => game.Publish(lockToken: token, actorId: ACTOR, now: NOW);

                        // Assert
                        Assert.Throws<DomainValidationException>(testCode: act);
                }

                /// <summary>A complete sorting game can be published.</summary>
                [Fact]
                public void PublishSucceedsWhenGameIsComplete()
                {
                        // Arrange
                        SortingGameEntity game = CreateLockedGame(lockToken: out Guid token);
                        game.ChangeTemplate(templateId: Guid.NewGuid(), lockToken: token, actorId: ACTOR, now: NOW);
                        CategoryReceiverEntity receiver = game.AddCategoryReceiver(name: "Pap", color: COLOR, imageId: null, lockToken: token, actorId: ACTOR, now: NOW);
                        game.AddCategoryItem(name: "Karton", explanation: "Pap.", imageId: Guid.NewGuid(), categoryReceiverId: receiver.Id, lockToken: token, actorId: ACTOR, now: NOW);

                        // Act
                        game.Publish(lockToken: token, actorId: ACTOR, now: NOW);

                        // Assert
                        Assert.Equal(expected: GameStatus.Published, actual: game.Status);
                }

                /// <summary>Adding something to a published game turns it back into a draft.</summary>
                [Fact]
                public void AddingToPublishedGameMakesItDraftAgain()
                {
                        // Arrange
                        SortingGameEntity game = CreateLockedGame(lockToken: out Guid token);
                        game.ChangeTemplate(templateId: Guid.NewGuid(), lockToken: token, actorId: ACTOR, now: NOW);
                        CategoryReceiverEntity receiver = game.AddCategoryReceiver(name: "Pap", color: COLOR, imageId: null, lockToken: token, actorId: ACTOR, now: NOW);
                        game.AddCategoryItem(name: "Karton", explanation: "Pap.", imageId: Guid.NewGuid(), categoryReceiverId: receiver.Id, lockToken: token, actorId: ACTOR, now: NOW);
                        game.Publish(lockToken: token, actorId: ACTOR, now: NOW);

                        // Act
                        game.AddCategoryReceiver(name: "Plast", color: COLOR, imageId: null, lockToken: token, actorId: ACTOR, now: NOW);

                        // Assert
                        Assert.Equal(expected: GameStatus.Draft, actual: game.Status);
                }
        }
}
