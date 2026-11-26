using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Sdk.Games.Responses
{
        /// <summary>
        /// The basic information about a game, sent back to the editor.
        /// </summary>
        /// <param name="Id">The unique identifier of the game.</param>
        /// <param name="Title">The title of the game.</param>
        /// <param name="Type">The kind of game, e.g. "SortingGame".</param>
        /// <param name="Status">The status of the game, e.g. "Draft".</param>
        public sealed record GameResponse(Guid Id, string Title, string Type, string Status);

}
