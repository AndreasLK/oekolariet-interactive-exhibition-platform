using EditorService.Facade.Commands.Games;

namespace EditorService.Application.Interfaces.CommandHandlers.Games
{
        /// <summary>
        /// Changes the template of an existing game.
        /// </summary>
        public interface IUpdateGameTemplateCommandHandler
        {
                /// <summary>
                /// Changes the template and saves it.
                /// </summary>
                /// <param name="command">The game, the lock token, the new template and who changes it.</param>
                /// <param name="cancellationToken">Stops the call if the request is cancelled.</param>
                /// <returns>A task that completes when the template is saved.</returns>
                public Task HandleAsync(UpdateGameTemplateCommand command, CancellationToken cancellationToken);
        }
}
