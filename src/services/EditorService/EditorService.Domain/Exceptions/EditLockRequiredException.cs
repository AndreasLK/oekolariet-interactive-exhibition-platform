using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Domain.Exceptions
{
        /// <summary>
        /// Thrown when a game is changed without holding a valid edit lock,
        /// for example because the lock was never taken, has expired, or belongs to another session.
        /// </summary>
        public sealed class EditLockRequiredException : DomainException
        {
                /// <summary>
                /// Initializes a new instance of the <see cref="EditLockRequiredException"/> class.
                /// </summary>
                /// <param name="message">The message that describes why the lock is missing.</param>
                public EditLockRequiredException(string message) : base(message: message)
                {
                }
        }
}
