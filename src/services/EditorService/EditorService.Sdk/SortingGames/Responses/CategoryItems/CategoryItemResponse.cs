using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Sdk.SortingGames.Responses.CategoryItems
{
        /// <summary>
        /// An item in a sorting game, sent to the editor.
        /// </summary>
        /// <param name="Id">The id of the item.</param>
        /// <param name="Name">The name of the item.</param>
        /// <param name="Explanation">The explanation shown after the item is sorted.</param>
        /// <param name="ImageId">The image of the item.</param>
        /// <param name="CategoryReceiverId">The category the item belongs to.</param>
        public sealed record CategoryItemResponse(Guid Id, string Name, string Explanation, Guid ImageId, Guid CategoryReceiverId);
}
