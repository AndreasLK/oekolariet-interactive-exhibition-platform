using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Facade.Commands.Games
{
        /// <summary>
        /// Asks to change the template of a game.
        /// </summary>
        /// <param name="GameId">The game to change.</param>
        /// <param name="LockToken">The edit lock token.</param>
        /// <param name="TemplateId">The new template.</param>
        /// <param name="ActorId">The employee making the change.</param>
        public sealed record UpdateGameTemplateCommand(Guid GameId, Guid LockToken, Guid TemplateId, string ActorId);
}
