using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Facade.Commands.EditLocks
{
        /// <summary>
        /// Asks to take the edit lock on a game.
        /// </summary>
        /// <param name="GameId">The game to lock.</param>
        /// <param name="HolderName">The logged-in employee. Comes from the JWT in the Api, never from the client.</param>
        /// <param name="DeviceName">The device the employee is using, e.g. "Kontor-PC 2".</param>
        public sealed record AcquireLockCommand(Guid GameId, string HolderName, string DeviceName);
}
