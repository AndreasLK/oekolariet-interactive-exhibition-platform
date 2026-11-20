using EditorService.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;
using EditorService.Domain.Exceptions;

namespace EditorService.Domain.Entities
{
        /// <summary>
        /// Represents a time-limited edit lock on a game (F8).
        /// The lock is part of the game aggregate and is only created and changed through <see cref="GameEntity"/>.
        /// </summary>
        public sealed class EditLockEntity
        {
                private EditLockEntity()
                {
                }

                /// <summary>Gets the secret token the editor session must send with every change while it holds the lock.</summary>
                public Guid LockToken { get; private set; }

                /// <summary>Gets the name of the person holding the lock.</summary>
                public string HolderName { get; private set; } = string.Empty;

                /// <summary>Gets the name of the device the lock is held from.</summary>
                public string DeviceName { get; private set; } = string.Empty;

                /// <summary>Gets the UTC time the lock was first taken.</summary>
                public DateTimeOffset LockedAt { get; private set; }

                /// <summary>Gets the UTC time the lock expires unless it is renewed.</summary>
                public DateTimeOffset ExpiresAt { get; private set; }

                /// <summary>
                /// Creates a new lock that is valid for <see cref="DomainConstants.EDIT_LOCK_DURATION_MINUTES"/> minutes.
                /// </summary>
                /// <param name="holderName">The name of the person taking the lock.</param>
                /// <param name="deviceName">The device the lock is taken from.</param>
                /// <param name="now">The current UTC time.</param>
                /// <returns>A new active <see cref="EditLockEntity"/>.</returns>
                internal static EditLockEntity Create(string holderName, string deviceName, DateTimeOffset now)
                {
                        EditLockEntity editLock = new()
                        {
                                // A new random token that only this editor session knows.
                                LockToken = Guid.NewGuid(),
                                HolderName = ValidateName(value: holderName, maxLength: DomainConstants.MAX_LOCK_HOLDER_NAME_LENGTH, fieldName: "Holder name"),
                                DeviceName = ValidateName(value: deviceName, maxLength: DomainConstants.MAX_LOCK_DEVICE_NAME_LENGTH, fieldName: "Device name"),
                                LockedAt = now,
                                // The lock runs out after 15 minutes unless the editor renews it.
                                ExpiresAt = now.AddMinutes(minutes: DomainConstants.EDIT_LOCK_DURATION_MINUTES)
                        };

                        return editLock;
                }

                /// <summary>
                /// Determines whether the lock is still valid at the given time.
                /// </summary>
                /// <param name="now">The current UTC time.</param>
                /// <returns><see langword="true"/> if the lock has not expired; otherwise <see langword="false"/>.</returns>
                public bool IsActive(DateTimeOffset now)
                {
                        // The lock is active as long as the expiry time has not been reached.
                        return now < this.ExpiresAt;
                }

                /// <summary>
                /// Determines whether the lock is held by the same person on the same device.
                /// </summary>
                /// <param name="holderName">The name to compare with.</param>
                /// <param name="deviceName">The device to compare with.</param>
                /// <returns><see langword="true"/> if both name and device match; otherwise <see langword="false"/>.</returns>
                public bool IsHeldBy(string holderName, string deviceName)
                {
                        // Compare without caring about upper/lower case, so "Lise" and "lise" count as the same person.
                        bool sameHolder = string.Equals(a: this.HolderName, b: holderName.Trim(), comparisonType: StringComparison.OrdinalIgnoreCase);
                        bool sameDevice = string.Equals(a: this.DeviceName, b: deviceName.Trim(), comparisonType: StringComparison.OrdinalIgnoreCase);
                        return sameHolder && sameDevice;
                }

                /// <summary>
                /// Extends the lock so it expires <see cref="DomainConstants.EDIT_LOCK_DURATION_MINUTES"/> minutes from now.
                /// </summary>
                /// <param name="now">The current UTC time.</param>
                internal void Renew(DateTimeOffset now)
                {
                        // Push the expiry time 15 minutes forward from now.
                        this.ExpiresAt = now.AddMinutes(minutes: DomainConstants.EDIT_LOCK_DURATION_MINUTES);
                }

                private static string ValidateName(string value, int maxLength, string fieldName)
                {
                        if (string.IsNullOrWhiteSpace(value: value))
                        {
                                throw new DomainValidationException(message: $"{fieldName} cannot be null or whitespace.");
                        }

                        string trimmed = value.Trim();
                        if (trimmed.Length > maxLength)
                        {
                                throw new DomainValidationException(message: $"{fieldName} cannot exceed {maxLength} characters.");
                        }

                        return trimmed;
                }
        }
}
