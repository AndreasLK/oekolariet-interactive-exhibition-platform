using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Sdk.Games.Requests
{
        /// <summary>
        /// The body sent by the editor when a game gets a new title.
        /// </summary>
        /// <param name="Title">The new title of the game.</param>
        public sealed record RenameGameRequest(string Title);
}
