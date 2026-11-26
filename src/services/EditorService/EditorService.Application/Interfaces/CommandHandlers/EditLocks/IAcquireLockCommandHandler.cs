using EditorService.Facade.Commands.EditLocks;
using EditorService.Facade.DTOs.EditLocks;
using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Application.Interfaces.CommandHandlers.EditLocks
{
        /// <summary>
        /// Takes the edit lock on a game, so only one employee can edit it at a time.
        /// </summary>
        public interface IAcquireLockCommandHandler
        {
                /// <summary>
                /// Takes the lock and saves it. Renews it if the same employee on the same device already holds it.
                /// </summary>
                /// <param name="command">The game, the employee and the device.</param>
                /// <param name="cancellationToken">Stops the call if the request is cancelled.</param>
                /// <returns>The lock token and when the lock expires.</returns>
                public Task<EditLockDto> HandleAsync(AcquireLockCommand command, CancellationToken cancellationToken);
        }
}
