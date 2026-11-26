using EditorService.Facade.Commands.EditLocks;
using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Application.Interfaces.CommandHandlers.EditLocks
{
        /// <summary>
        /// Gives the edit lock on a game free.
        /// </summary>
        public interface IReleaseLockCommandHandler
        {
                /// <summary>
                /// Releases the lock and saves the change. Does nothing if the token is wrong.
                /// </summary>
                /// <param name="command">The game and the lock token.</param>
                /// <param name="cancellationToken">Stops the call if the request is cancelled.</param>
                /// <returns>A task that completes when the lock is released.</returns>
                public Task HandleAsync(ReleaseLockCommand command, CancellationToken cancellationToken);
        }
}
