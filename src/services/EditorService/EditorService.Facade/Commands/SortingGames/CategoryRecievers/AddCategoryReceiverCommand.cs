using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Facade.Commands.SortingGames.CategoryRecievers
{
        /// <summary>
        /// Asks to add a category (receiver) to a sorting game.
        /// </summary>
        /// <param name="GameId">The sorting game.</param>
        /// <param name="LockToken">The edit lock token.</param>
        /// <param name="Name">The name of the category, e.g. "Plast".</param>
        /// <param name="Color">The color of the category, or null.</param>
        /// <param name="ImageId">The image of the category in Content Service, or null.</param>
        /// <param name="ActorId">The employee making the change.</param>
        public sealed record AddCategoryReceiverCommand(Guid GameId, Guid LockToken, string Name, string? Color, Guid? ImageId, string ActorId);
}
