using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Sdk.SortingGames.Requests.CategoryItems
{
        /// <summary>
        /// The body sent by the editor when an item is changed. The item stays in its category.
        /// </summary>
        /// <param name="Name">The new name.</param>
        /// <param name="Explanation">The new explanation.</param>
        /// <param name="ImageId">The new image.</param>
        public sealed record UpdateCategoryItemRequest(string Name, string Explanation, Guid ImageId);
}
