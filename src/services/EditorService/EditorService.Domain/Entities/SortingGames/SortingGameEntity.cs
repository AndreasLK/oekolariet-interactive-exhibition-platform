using EditorService.Domain.Enums;
using EditorService.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Domain.Entities.SortingGames
{
        /// <summary>
        /// A drag-and-drop game where the player sorts items into category receivers.
        /// Aggregate root: category receivers and category items are only added and changed through this class.
        /// </summary>
        public sealed class SortingGameEntity : GameEntity
        {
                // The category receivers and category items belong to this game. Only the game itself can add or remove them.
                private readonly List<CategoryReceiverEntity> _categoryReceivers = [];
                private readonly List<CategoryItemEntity> _categoryItems = [];

                /// <summary>
                /// Initializes a new instance of the <see cref="SortingGameEntity"/> class for EF Core.
                /// Use <see cref="Create"/> to make a new sorting game.
                /// </summary>
                private SortingGameEntity()
                {
                }

                /// <summary>Gets the kind of game, which is always a sorting game.</summary>
                public override GameType Type => GameType.SortingGame;

                /// <summary>Gets the category receivers in the game. Read-only from the outside.</summary>
                public IReadOnlyList<CategoryReceiverEntity> CategoryReceivers => this._categoryReceivers;

                /// <summary>Gets the category items in the game. Read-only from the outside.</summary>
                public IReadOnlyList<CategoryItemEntity> CategoryItems => this._categoryItems;

                /// <summary>
                /// Creates a new sorting game as a draft.
                /// </summary>
                /// <param name="title">The title of the game.</param>
                /// <param name="actorId">The identifier of the actor creating the game.</param>
                /// <param name="now">The current UTC time.</param>
                /// <returns>A new <see cref="SortingGameEntity"/>.</returns>
                public static SortingGameEntity Create(string title, string actorId, DateTimeOffset now)
                {
                        SortingGameEntity game = new();
                        game.InitializeGame(title: title, actorId: actorId, now: now);
                        return game;
                }

                /// <summary>
                /// Checks that the sorting game is complete before it can be published.
                /// </summary>
                protected override void EnsureReadyToPublish()
                {
                        // A sorting game needs at least one category receiver and one category item before it can be shown on a screen.
                        if (this._categoryReceivers.Count == 0)
                        {
                                throw new DomainValidationException(message: "A sorting game must have at least one category receiver before it can be published.");
                        }

                        if (this._categoryItems.Count == 0)
                        {
                                throw new DomainValidationException(message: "A sorting game must have at least one category item before it can be published.");
                        }
                }

                /// <summary>
                /// Adds a new category receiver, e.g. a bin, to the game.
                /// </summary>
                /// <param name="name">The name of the category receiver.</param>
                /// <param name="color">The color of the category receiver, or <see langword="null"/>.</param>
                /// <param name="imageId">The identifier of the image in Content Service, or <see langword="null"/>.</param>
                /// <param name="lockToken">The token of the lock held by the caller.</param>
                /// <param name="actorId">The identifier of the actor making the change.</param>
                /// <param name="now">The current UTC time.</param>
                /// <returns>The new <see cref="CategoryReceiverEntity"/>.</returns>
                public CategoryReceiverEntity AddCategoryReceiver(string name, string? color, Guid? imageId, Guid lockToken, string actorId, DateTimeOffset now)
                {
                        // 1. Only the person holding the lock may change the game.
                        this.EnsureLockHeld(lockToken: lockToken, now: now);

                        // 2. Create the category receiver. It checks its own values.
                        CategoryReceiverEntity categoryReceiver = CategoryReceiverEntity.Create(name: name, color: color, imageId: imageId);

                        // 3. Add it to the game and mark the game as changed.
                        this._categoryReceivers.Add(item: categoryReceiver);
                        this.MarkEdited(actorId: actorId, now: now);

                        return categoryReceiver;
                }

                /// <summary>
                /// Adds a new category item, e.g. a piece of waste, to the game.
                /// </summary>
                /// <param name="name">The name of the category item.</param>
                /// <param name="explanation">The explanation shown to the player.</param>
                /// <param name="imageId">The identifier of the image in Content Service.</param>
                /// <param name="categoryReceiverId">The identifier of the correct category receiver in this game.</param>
                /// <param name="lockToken">The token of the lock held by the caller.</param>
                /// <param name="actorId">The identifier of the actor making the change.</param>
                /// <param name="now">The current UTC time.</param>
                /// <returns>The new <see cref="CategoryItemEntity"/>.</returns>
                /// <exception cref="DomainValidationException">Thrown when the category receiver does not exist in this game.</exception>
                public CategoryItemEntity AddCategoryItem(string name, string explanation, Guid imageId, Guid categoryReceiverId, Guid lockToken, string actorId, DateTimeOffset now)
                {
                        // 1. Only the person holding the lock may change the game.
                        this.EnsureLockHeld(lockToken: lockToken, now: now);

                        // 2. The item must point to a category receiver that exists in this game.
                        bool receiverExists = this._categoryReceivers.Any(predicate: receiver => receiver.Id == categoryReceiverId);
                        if (!receiverExists)
                        {
                                throw new DomainValidationException(message: "The category receiver does not exist in this game.");
                        }

                        // 3. Create the category item. It checks its own values.
                        CategoryItemEntity categoryItem = CategoryItemEntity.Create(name: name, explanation: explanation, imageId: imageId, categoryReceiverId: categoryReceiverId);

                        // 4. Add it to the game and mark the game as changed.
                        this._categoryItems.Add(item: categoryItem);
                        this.MarkEdited(actorId: actorId, now: now);

                        return categoryItem;
                }
                /// <summary>
                /// Removes a category receiver and all category items that belong to it.
                /// </summary>
                /// <param name="categoryReceiverId">The identifier of the category receiver to remove.</param>
                /// <param name="lockToken">The token of the lock held by the caller.</param>
                /// <param name="actorId">The identifier of the actor making the change.</param>
                /// <param name="now">The current UTC time.</param>
                /// <exception cref="DomainValidationException">Thrown when the category receiver does not exist in this game.</exception>
                public void RemoveCategoryReceiver(Guid categoryReceiverId, Guid lockToken, string actorId, DateTimeOffset now)
                {
                        // 1. Only the person holding the lock may change the game.
                        this.EnsureLockHeld(lockToken: lockToken, now: now);
                        // 2. Find the category receiver in the game.
                        CategoryReceiverEntity? categoryReceiver = this._categoryReceivers.FirstOrDefault(predicate: receiver => receiver.Id == categoryReceiverId);
                        if (categoryReceiver is null)
                        {
                                throw new DomainValidationException(message: "The category receiver does not exist in this game.");
                        }
                        // 3. Remove all items that belong to this category receiver.
                        this._categoryItems.RemoveAll(match: item => item.CategoryReceiverId == categoryReceiverId);
                        // 4. Remove the category receiver itself and mark the game as changed.
                        this._categoryReceivers.Remove(item: categoryReceiver);
                        this.MarkEdited(actorId: actorId, now: now);
                }

                /// <summary>
                /// Removes a category item from the game.    
                /// </summary>
                /// <param name="categoryItemId">The identifier of the category item to remove.</param>
                /// <param name="lockToken">The token of the lock held by the caller.</param>
                /// <param name="actorId">The identifier of the actor making the change.</param>
                /// <param name="now">The current UTC time.</param>
                /// <exception cref="DomainValidationException">Thrown when the category item does not exist in this game.</exception>
                public void RemoveCategoryItem(Guid categoryItemId, Guid lockToken, string actorId, DateTimeOffset now)
                {
                        // 1. Only the person holding the lock may change the game.
                        this.EnsureLockHeld(lockToken: lockToken, now: now);
                        // 2. Find the category item in the game.
                        CategoryItemEntity? categoryItem = this._categoryItems.FirstOrDefault(predicate: item => item.Id == categoryItemId);
                        if (categoryItem is null)
                        {
                                throw new DomainValidationException(message: "The category item does not exist in this game.");
                        }
                        // 3. Remove the category item and mark the game as changed.
                        this._categoryItems.Remove(item: categoryItem);
                        this.MarkEdited(actorId: actorId, now: now);
                }

                /// <summary>
                /// Updates a Category Reciever
                /// </summary>
                /// <param name="categoryReceiverId"></param>
                /// <param name="name"></param>
                /// <param name="color"></param>
                /// <param name="imageId"></param>
                /// <param name="lockToken"></param>
                /// <param name="actorId"></param>
                /// <param name="now"></param>
                /// <exception cref="DomainValidationException"></exception>
                public void UpdateCategoryReceiver(Guid categoryReceiverId, string name, string? color, Guid? imageId, Guid lockToken, string actorId, DateTimeOffset now)
                {
                        // 1. Only the person holding the lock may change the game.
                        this.EnsureLockHeld(lockToken: lockToken, now: now);
                        // 2. Find the category receiver in the game.
                        CategoryReceiverEntity? categoryReceiver = this._categoryReceivers.FirstOrDefault(predicate: receiver => receiver.Id == categoryReceiverId);
                        if (categoryReceiver is null)
                        {
                                throw new DomainValidationException(message: "The category receiver does not exist in this game.");
                        }
                        // 3. Update the category receiver and mark the game as changed.
                        categoryReceiver.UpdateName(name: name);
                        categoryReceiver.UpdateAppearance(color: color, imageId: imageId);
                        this.MarkEdited(actorId: actorId, now: now);
                }

                /// <summary>
                /// Updates a Category Item
                /// </summary>
                /// <param name="categoryItemId"></param>
                /// <param name="name"></param>
                /// <param name="explanation"></param>
                /// <param name="imageId"></param>
                /// <param name="categoryReceiverId"></param>
                /// <param name="lockToken"></param>
                /// <param name="actorId"></param>
                /// <param name="now"></param>
                /// <exception cref="DomainValidationException"></exception>
                public void UpdateCategoryItem(Guid categoryItemId, string name, string explanation, Guid imageId, Guid categoryReceiverId, Guid lockToken, string actorId, DateTimeOffset now)
                {
                        // 1. Only the person holding the lock may change the game.
                        this.EnsureLockHeld(lockToken: lockToken, now: now);
                        // 2. Find the category item in the game.
                        CategoryItemEntity? categoryItem = this._categoryItems.FirstOrDefault(predicate: item => item.Id == categoryItemId);
                        if (categoryItem is null)
                        {
                                throw new DomainValidationException(message: "The category item does not exist in this game.");
                        }
                        // 3. The item must point to a category receiver that exists in this game.
                        bool receiverExists = this._categoryReceivers.Any(predicate: receiver => receiver.Id == categoryReceiverId);
                        if (!receiverExists)
                        {
                                throw new DomainValidationException(message: "The category receiver does not exist in this game.");
                        }
                        // 4. Update the category item and mark the game as changed.
                        categoryItem.UpdateName(name: name);
                        categoryItem.UpdateExplanation(explanation: explanation);
                        categoryItem.UpdateImage(imageId: imageId);
                        this.MarkEdited(actorId: actorId, now: now);
                }
        }
}
