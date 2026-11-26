using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Facade.Commands.SortingGames.CategoryItems
{
        /// <summary>
        /// Asks to remove an item from a sorting game.
        /// </summary>
        /// <param name="GameId">The sorting game.</param>
        /// <param name="CategoryItemId">The item to remove.</param>
        /// <param name="LockToken">The edit lock token.</param>
        /// <param name="ActorId">The employee making the change.</param>
        public sealed record RemoveCategoryItemCommand(Guid GameId, Guid CategoryItemId, Guid LockToken, string ActorId);
}
