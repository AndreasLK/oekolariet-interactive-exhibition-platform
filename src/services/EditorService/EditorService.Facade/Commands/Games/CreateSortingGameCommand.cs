using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Facade.Commands.Games
{
        /// <summary>
        /// Asks to create a new sorting game.
        /// </summary>
        /// <param name="Title">The title of the new game.</param>
        /// <param name="ActorId">The identifier of the employee creating it. Comes from the JWT in the Api.</param>
        public sealed record CreateSortingGameCommand(string Title, string ActorId);
}
