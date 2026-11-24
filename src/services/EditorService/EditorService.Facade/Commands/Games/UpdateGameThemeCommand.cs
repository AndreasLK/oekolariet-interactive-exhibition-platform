using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Facade.Commands.Games
{
        /// <summary>
        /// Asks to change the theme of a game.
        /// </summary>
        /// <param name="GameId">The game to change.</param>
        /// <param name="LockToken">The edit lock token.</param>
        /// <param name="ThemeId">The new theme.</param>
        /// <param name="ActorId">The employee making the change.</param>
        public sealed record UpdateGameThemeCommand(Guid GameId, Guid LockToken, Guid ThemeId, string ActorId);
}
