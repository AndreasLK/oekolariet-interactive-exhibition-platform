using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Facade.Commands.SortingGames.CategoryRecievers
{
        /// <summary>
        /// Asks to change the name, color and image of a category.
        /// </summary>
        /// <param name="GameId">The sorting game.</param>
        /// <param name="CategoryReceiverId">The category to change.</param>
        /// <param name="LockToken">The edit lock token.</param>
        /// <param name="Name">The new name.</param>
        /// <param name="Color">The new color, or null.</param>
        /// <param name="ImageId">The new image, or null.</param>
        /// <param name="ActorId">The employee making the change.</param>
        public sealed record UpdateCategoryReceiverCommand(Guid GameId, Guid CategoryReceiverId, Guid LockToken, string Name, string? Color, Guid? ImageId, string ActorId);
}
