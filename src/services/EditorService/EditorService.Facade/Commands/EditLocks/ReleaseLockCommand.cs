using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Facade.Commands.EditLocks
{
        /// <summary>
        /// Asks to give the edit lock on a game free.
        /// </summary>
        /// <param name="GameId">The locked game.</param>
        /// <param name="LockToken">The token from when the lock was taken.</param>
        public sealed record ReleaseLockCommand(Guid GameId, Guid LockToken);
}
