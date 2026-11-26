using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Sdk.SortingGames.Requests.CategoryRecievers
{
        /// <summary>
        /// The body sent by the editor when a category is changed.
        /// </summary>
        /// <param name="Name">The new name.</param>
        /// <param name="Color">The new color, or null.</param>
        /// <param name="ImageId">The new image, or null.</param>
        public sealed record UpdateCategoryReceiverRequest(string Name, string? Color, Guid? ImageId);
}
