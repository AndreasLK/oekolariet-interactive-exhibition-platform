using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Sdk.Games.Requests
{
        /// <summary>
        /// The body sent by the editor when a game gets a new theme.
        /// </summary>
        /// <param name="ThemeId">The theme to use.</param>
        public sealed record UpdateGameThemeRequest(Guid ThemeId);
}
