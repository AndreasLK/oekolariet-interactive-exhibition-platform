using EditorService.Facade.Commands.Games;
using EditorService.Facade.DTOs.Games;
using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Application.Interfaces.CommandHandlers.Games
{
        /// <summary>
        /// Gives an existing game a new title.
        /// </summary>
        public interface IRenameGameCommandHandler
        {
                /// <summary>
                /// Renames the game and saves it.
                /// </summary>
                /// <param name="command">The game, the lock token, the new title and who renames it.</param>
                /// <param name="cancellationToken">Stops the call if the request is cancelled.</param>
                /// <returns>The game with its new title.</returns>
                public Task<GameDto> HandleAsync(RenameGameCommand command, CancellationToken cancellationToken);
        }
}
