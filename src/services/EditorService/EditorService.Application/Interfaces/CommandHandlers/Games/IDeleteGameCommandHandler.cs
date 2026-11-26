using EditorService.Facade.Commands.Games;
using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Application.Interfaces.CommandHandlers.Games
{
        /// <summary>
        /// Deletes an existing game with all its content.
        /// </summary>
        public interface IDeleteGameCommandHandler
        {
                /// <summary>
                /// Deletes the game and saves the change.
                /// </summary>
                /// <param name="command">The game, the lock token and who deletes it.</param>
                /// <param name="cancellationToken">Stops the call if the request is cancelled.</param>
                /// <returns>A task that completes when the game is deleted.</returns>
                public Task HandleAsync(DeleteGameCommand command, CancellationToken cancellationToken);
        }
}
