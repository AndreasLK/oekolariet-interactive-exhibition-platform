using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Facade.Commands.SortingGames.CategoryItems
{
        /// <summary>
        /// Asks to change the name, explanation and image of an item. The item stays in its category.
        /// </summary>
        /// <param name="GameId">The sorting game.</param>
        /// <param name="CategoryItemId">The item to change.</param>
        /// <param name="LockToken">The edit lock token.</param>
        /// <param name="Name">The new name.</param>
        /// <param name="Explanation">The new explanation.</param>
        /// <param name="ImageId">The new image.</param>
        /// <param name="ActorId">The employee making the change.</param>
        public sealed record UpdateCategoryItemCommand(Guid GameId, Guid CategoryItemId, Guid LockToken, string Name, string Explanation, Guid ImageId, string ActorId);
}
