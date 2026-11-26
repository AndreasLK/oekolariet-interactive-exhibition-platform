using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Facade.Commands.SortingGames.CategoryRecievers
{
        /// <summary>
        /// Asks to remove a category and all its items.
        /// </summary>
        /// <param name="GameId">The sorting game.</param>
        /// <param name="CategoryReceiverId">The category to remove.</param>
        /// <param name="LockToken">The edit lock token.</param>
        /// <param name="ActorId">The employee making the change.</param>
        public sealed record RemoveCategoryReceiverCommand(Guid GameId, Guid CategoryReceiverId, Guid LockToken, string ActorId);
}
