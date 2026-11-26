using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Sdk.SortingGames.Requests.CategoryRecievers
{
        /// <summary>
        /// The body sent by the editor when a new category is added to a sorting game.
        /// </summary>
        /// <param name="Name">The name of the category, e.g. "Plast".</param>
        /// <param name="Color">The color of the category, or null.</param>
        /// <param name="ImageId">The image of the category in Content Service, or null.</param>
        public sealed record AddCategoryReceiverRequest(string Name, string? Color, Guid? ImageId);
}
