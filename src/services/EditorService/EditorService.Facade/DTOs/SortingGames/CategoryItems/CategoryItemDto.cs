using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Facade.DTOs.SortingGames.CategoryItems
{
        /// <summary>
        /// An item the visitor sorts into a category, e.g. "Mælkekarton".
        /// </summary>
        public sealed record CategoryItemDto
        {
                /// <summary>The id of the item.</summary>
                public required Guid Id { get; init; }

                /// <summary>The name of the item.</summary>
                public required string Name { get; init; }

                /// <summary>The explanation shown after the item is sorted.</summary>
                public required string Explanation { get; init; }

                /// <summary>The image of the item in Content Service.</summary>
                public required Guid ImageId { get; init; }

                /// <summary>The category the item belongs to.</summary>
                public required Guid CategoryReceiverId { get; init; }
        }
}
