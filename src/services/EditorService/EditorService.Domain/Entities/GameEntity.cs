using System;
using System.Collections.Generic;
using System.Security.AccessControl;
using System.Text;
using EditorService.Domain.Common;
using EditorService.Domain.Enums;
using EditorService.Domain.Exceptions;


namespace EditorService.Domain.Entities
{
        /// <summary>
        /// Base class and aggregate root for every game built in the editor.
        /// Holds what all games share: title, status, template, theme and the edit lock.
        /// Concrete games (<c>SortingGameEntity</c>, <c>QuizEntity</c>) inherit from this class.
        /// </summary>
        public abstract class GameEntity : BaseEntity
        {
                /// <summary>
                /// Initializes a new instance of the <see cref="GameEntity"/> class for ORM materialization.
                /// </summary>
                protected GameEntity()
                {
                }
                public abstract GameType Type { get; }
                public string Title { get; private set; } = string.Empty;
                public GameStatus Status { get; private set; }
                public Guid? TemplateId { get; private set; }
                public Guid? ThemeId { get; private set; }
                public DateTimeOffset? PublishedAt { get; private set; }
                public EditLockEntity? EditLock { get; private set; }

                /// <summary>
                /// Determines whether the game is currently locked for editing by another user.
                /// </summary>
                /// <param name="now"></param>
                /// <returns></returns>
                public bool IsLocked(DateTimeOffset now)
                {
                        return this.EditLock is not null && this.EditLock.IsActive(now: now);
                }

                /// <summary>
                /// Takes the edit lock. If the same person on the same device already holds it, the lock is renewed.
                /// </summary>
                /// <param name="holderName">The name of the person editing.</param>
                /// <param name="deviceName">The device the person is editing from.</param>
                /// <param name="now">The current UTC time.</param>
                /// <returns>The lock token the editor session must send with every change.</returns>
                /// <exception cref="GameLockedException">Thrown when someone else holds an active lock.</exception>
                public Guid AcquireLock(string holderName, string deviceName, DateTimeOffset now)
                {
                        // 1. No lock, or the lock has expired: create a new lock.
                        if (this.EditLock is null || !this.EditLock.IsActive(now: now))
                        {
                                this.EditLock = EditLockEntity.Create(holderName: holderName, deviceName: deviceName, now: now);
                                return this.EditLock.LockToken;
                        }

                        // 2. Someone else holds the lock: stop with an error that says who.
                        if (!this.EditLock.IsHeldBy(holderName: holderName, deviceName: deviceName))
                        {
                                throw new GameLockedException(
                                        message: $"Game is locked by {this.EditLock.HolderName} on {this.EditLock.DeviceName} since {this.EditLock.LockedAt:u}.");
                        }

                        // 3. The caller already holds the lock: renew it and return the same token.
                        this.EditLock.Renew(now: now);
                        return this.EditLock.LockToken;

                }

                /// <summary>
                /// Extends the edit lock. The editor calls this regularly while the game is open.
                /// </summary>
                /// <param name="lockToken">The token returned by <see cref="AcquireLock"/>.</param>
                /// <param name="now">The current UTC time.</param>
                public void RenewLock(Guid lockToken, DateTimeOffset now)
                {
                        EditLockEntity editLock = this.EnsureLockHeld(lockToken: lockToken, now: now);
                        editLock.Renew(now: now);
                }

                /// <summary>
                /// Releases the edit lock. Does nothing if the token does not match the current lock.
                /// </summary>
                /// <param name="lockToken">The token returned by <see cref="AcquireLock"/>.</param>
                public void ReleaseLock(Guid lockToken)
                {
                        if (this.EditLock is null || this.EditLock.LockToken != lockToken)
                        {
                                return;
                        }

                        this.EditLock = null;
                }

                /// <summary>
                /// Changes the title of the game.
                /// </summary>
                /// <param name="title">The new title.</param>
                /// <param name="lockToken">The token of the lock held by the caller.</param>
                /// <param name="actorId">The identifier of the actor making the change.</param>
                /// <param name="now">The current UTC time.</param>
                public void Rename(string title, Guid lockToken, string actorId, DateTimeOffset now)
                {
                        this.EnsureLockHeld(lockToken: lockToken, now: now);
                        this.Title = ValidateTitle(title: title);
                        this.MarkEdited(actorId: actorId, now: now);
                }

                /// <summary>
                /// Chooses the template the game is laid out in.
                /// </summary>
                /// <param name="templateId">The identifier of the template in Content Service.</param>
                /// <param name="lockToken">The token of the lock held by the caller.</param>
                /// <param name="actorId">The identifier of the actor making the change.</param>
                /// <param name="now">The current UTC time.</param>
                public void ChangeTemplate(Guid templateId, Guid lockToken, string actorId, DateTimeOffset now)
                {
                        this.EnsureLockHeld(lockToken: lockToken, now: now);
                        this.TemplateId = ValidateReference(id: templateId, fieldName: "Template ID");
                        this.MarkEdited(actorId: actorId, now: now);
                }

                /// <summary>
                /// Chooses the theme (frame) of the game (F12).
                /// </summary>
                /// <param name="themeId">The identifier of the theme in Content Service.</param>
                /// <param name="lockToken">The token of the lock held by the caller.</param>
                /// <param name="actorId">The identifier of the actor making the change.</param>
                /// <param name="now">The current UTC time.</param>
                public void ChangeTheme(Guid themeId, Guid lockToken, string actorId, DateTimeOffset now)
                {
                        this.EnsureLockHeld(lockToken: lockToken, now: now);
                        this.ThemeId = ValidateReference(id: themeId, fieldName: "Theme ID");
                        this.MarkEdited(actorId: actorId, now: now);
                }

                /// <summary>
                /// Marks the game as published after checking that it is complete (F10).
                /// Sending it to the screens is done by the application layer.
                /// </summary>
                /// <param name="lockToken">The token of the lock held by the caller.</param>
                /// <param name="actorId">The identifier of the actor publishing.</param>
                /// <param name="now">The current UTC time.</param>
                public void Publish(Guid lockToken, string actorId, DateTimeOffset now)
                {
                        this.EnsureLockHeld(lockToken: lockToken, now: now);

                        if (this.TemplateId is null)
                        {
                                throw new DomainValidationException(message: "A template must be chosen before the game can be published.");
                        }

                        this.EnsureReadyToPublish();

                        this.Status = GameStatus.Published;
                        this.PublishedAt = now;
                        this.MarkModified(actorId: actorId, now: now);
                }

                /// <summary>
                /// Sets up the shared state of a new game. Called from the <c>Create</c> method of each concrete game.
                /// </summary>
                /// <param name="title">The display title of the game.</param>
                /// <param name="actorId">The identifier of the actor creating the game.</param>
                /// <param name="now">The current UTC time.</param>
                protected void InitializeGame(string title, string actorId, DateTimeOffset now)
                {
                        this.Title = ValidateTitle(title: title);
                        this.Status = GameStatus.Draft;
                        this.InitializeAudit(actorId: actorId, now: now);
                }

                /// <summary>
                /// Throws if the caller does not hold an active lock with the given token.
                /// Every method that changes the game, including those in concrete games, must call this first.
                /// </summary>
                /// <param name="lockToken">The token of the lock held by the caller.</param>
                /// <param name="now">The current UTC time.</param>
                /// <returns>The active lock.</returns>
                /// <exception cref="EditLockRequiredException">Thrown when the caller does not hold a valid lock.</exception>
                protected EditLockEntity EnsureLockHeld(Guid lockToken, DateTimeOffset now)
                {
                        if (this.EditLock is null || !this.EditLock.IsActive(now: now))
                        {
                                throw new EditLockRequiredException(message: "The game must be locked before it can be changed.");
                        }

                        if (this.EditLock.LockToken != lockToken)
                        {
                                throw new EditLockRequiredException(message: "The edit lock belongs to another editor session.");
                        }

                        return this.EditLock;
                }

                /// <summary>
                /// Records that the game has changed since it was last published.
                /// </summary>
                /// <param name="actorId">The identifier of the actor making the change.</param>
                /// <param name="now">The current UTC time.</param>
                protected void MarkEdited(string actorId, DateTimeOffset now)
                {
                        this.Status = GameStatus.Draft;
                        this.MarkModified(actorId: actorId, now: now);
                }

                /// <summary>
                /// Checks the rules that are specific to the concrete game before it can be published,
                /// for example that a quiz has questions. Throws <see cref="DomainValidationException"/> if not.
                /// </summary>
                protected abstract void EnsureReadyToPublish();

                private static string ValidateTitle(string title)
                {
                        if (string.IsNullOrWhiteSpace(value: title))
                        {
                                throw new DomainValidationException(message: "Game title cannot be null or whitespace.");
                        }

                        string trimmed = title.Trim();
                        if (trimmed.Length > DomainConstants.MAX_GAME_TITLE_LENGTH)
                        {
                                throw new DomainValidationException(
                                        message: $"Game title cannot exceed {DomainConstants.MAX_GAME_TITLE_LENGTH} characters.");
                        }

                        return trimmed;
                }

                private static Guid ValidateReference(Guid id, string fieldName)
                {
                        if (id == Guid.Empty)
                        {
                                throw new DomainValidationException(message: $"{fieldName} cannot be empty.");
                        }

                        return id;
                }
        }
}
