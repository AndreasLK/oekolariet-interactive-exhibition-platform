using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Facade.Commands.SortingGames.CategoryItems
{
        /// <summary>
        /// Asks to add an item to a category in a sorting game.
        /// </summary>
        /// <param name="GameId">The sorting game.</param>
        /// <param name="LockToken">The edit lock token.</param>
        /// <param name="CategoryReceiverId">The category the item belongs to.</param>
        /// <param name="Name">The name of the item, e.g. "Mælkekarton".</param>
        /// <param name="Explanation">The explanation shown after the item is sorted.</param>
        /// <param name="ImageId">The image of the item in Content Service.</param>
        /// <param name="ActorId">The employee making the change.</param>
        public sealed record AddCategoryItemCommand(Guid GameId, Guid LockToken, Guid CategoryReceiverId, string Name, string Explanation, Guid ImageId, string ActorId);
}
