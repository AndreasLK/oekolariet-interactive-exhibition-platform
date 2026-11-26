using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Facade.Commands.EditLocks
{
        /// <summary>
        /// Asks to extend the edit lock on a game by 15 minutes.
        /// </summary>
        /// <param name="GameId">The locked game.</param>
        /// <param name="LockToken">The token from when the lock was taken.</param>
        public sealed record RenewLockCommand(Guid GameId, Guid LockToken);
}
