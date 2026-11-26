using EditorService.Facade.Commands.Games;
using EditorService.Facade.DTOs.Games;
using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Application.Interfaces.CommandHandlers.Games
{
        /// <summary>
        /// Publishes a game, so it can be shown in the exhibition.
        /// </summary>
        public interface IPublishGameCommandHandler
        {
                /// <summary>
                /// Publishes the game and saves it. The domain checks that the game is ready.
                /// </summary>
                /// <param name="command">The game, the lock token and who publishes it.</param>
                /// <param name="cancellationToken">Stops the call if the request is cancelled.</param>
                /// <returns>The game with its new status.</returns>
                public Task<GameDto> HandleAsync(PublishGameCommand command, CancellationToken cancellationToken);
        }
}
