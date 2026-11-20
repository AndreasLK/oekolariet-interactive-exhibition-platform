using System;
using System.Collections.Generic;
using System.Text;
using EditorService.Domain.Entities;

namespace EditorService.Domain.Exceptions
{
        /// <summary>
        /// Thrown when a game is locked by someone else.
        /// </summary>
        public sealed class GameLockedException : DomainException
        {
                /// <summary>
                /// Initializes a new instance of the <see cref="GameLockedException"/> class.
                /// </summary>
                /// <param name="message">The message that says who holds the lock.</param>
                public GameLockedException(string message) : base(message: message)
                {
                }
        }
}
