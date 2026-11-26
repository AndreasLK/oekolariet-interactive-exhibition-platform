using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Sdk.SortingGames.Requests.CategoryItems
{
        /// <summary>
        /// The body sent by the editor when a new item is added to a category.
        /// </summary>
        /// <param name="CategoryReceiverId">The category the item belongs to.</param>
        /// <param name="Name">The name of the item, e.g. "Mælkekarton".</param>
        /// <param name="Explanation">The explanation shown after the item is sorted.</param>
        /// <param name="ImageId">The image of the item in Content Service.</param>
        public sealed record AddCategoryItemRequest(Guid CategoryReceiverId, string Name, string Explanation, Guid ImageId);
}
