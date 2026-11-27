using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Facade.DTOs.SortingGames.CategoryRecievers
{
        /// <summary>
        /// A category in a sorting game, e.g. "Plast" or "Pap".
        /// </summary>
        public sealed record CategoryReceiverDto
        {
                /// <summary>The id of the category.</summary>
                public required Guid Id { get; init; }

                /// <summary>The name of the category.</summary>
                public required string Name { get; init; }

                /// <summary>The color of the category, or null.</summary>
                public string? Color { get; init; }

                /// <summary>The image of the category in Content Service, or null.</summary>
                public Guid? ImageId { get; init; }
        }
}
