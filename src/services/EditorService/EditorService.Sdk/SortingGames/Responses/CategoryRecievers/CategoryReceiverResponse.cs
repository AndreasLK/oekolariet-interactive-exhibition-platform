using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Sdk.SortingGames.Responses.CategoryRecievers
{
        /// <summary>
        /// A category in a sorting game, sent to the editor.
        /// </summary>
        /// <param name="Id">The id of the category.</param>
        /// <param name="Name">The name of the category.</param>
        /// <param name="Color">The color of the category, or null.</param>
        /// <param name="ImageId">The image of the category, or null.</param>
        public sealed record CategoryReceiverResponse(Guid Id, string Name, string? Color, Guid? ImageId);
}
