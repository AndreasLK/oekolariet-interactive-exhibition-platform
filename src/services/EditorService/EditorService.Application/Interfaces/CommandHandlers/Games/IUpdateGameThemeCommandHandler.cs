using System;
using System.Collections.Generic;
using System.Text;
using EditorService.Facade.Commands.Games;

namespace EditorService.Application.Interfaces.CommandHandlers.Games
{
        /// <summary>
        /// Changes the theme of an existing game.
        /// </summary>
        public interface IUpdateGameThemeCommandHandler
        {
                /// <summary>
                /// Changes the theme and saves it.
                /// </summary>
                /// <param name="command">The game, the lock token, the new theme and who changes it.</param>
                /// <param name="cancellationToken">Stops the call if the request is cancelled.</param>
                /// <returns>A task that completes when the theme is saved.</returns>
                public Task HandleAsync(UpdateGameThemeCommand command, CancellationToken cancellationToken);
        }
}
