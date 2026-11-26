using EditorService.Facade.Commands.EditLocks;
using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Application.Interfaces.CommandHandlers.EditLocks
{
        /// <summary>
        /// Extends the edit lock on a game by 15 minutes.
        /// </summary>
        public interface IRenewLockCommandHandler
        {
                /// <summary>
                /// Renews the lock and saves the new expiry time.
                /// </summary>
                /// <param name="command">The game and the lock token.</param>
                /// <param name="cancellationToken">Stops the call if the request is cancelled.</param>
                /// <returns>A task that completes when the lock is renewed.</returns>
                public Task HandleAsync(RenewLockCommand command, CancellationToken cancellationToken);
        }
}
