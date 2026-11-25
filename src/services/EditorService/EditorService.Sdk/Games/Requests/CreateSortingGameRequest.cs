using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Sdk.Games.Requests
{
        /// <summary>
        /// What the editor sends to create a new sorting game.
        /// </summary>
        /// <param name="Title">The title of the new game.</param>
        public sealed record CreateSortingGameRequest(string Title);
}
