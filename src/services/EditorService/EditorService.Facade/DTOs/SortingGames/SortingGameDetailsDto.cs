using EditorService.Facade.DTOs.SortingGames.CategoryItems;
using EditorService.Facade.DTOs.SortingGames.CategoryRecievers;
using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Facade.DTOs.SortingGames
{
        /// <summary>
        /// A sorting game with everything the editor needs to show it.
        /// </summary>
        public sealed record SortingGameDetailsDto
        {
                /// <summary>The id of the game.</summary>
                public required Guid Id { get; init; }

                /// <summary>The title of the game.</summary>
                public required string Title { get; init; }

                /// <summary>Draft or Published.</summary>
                public required string Status { get; init; }

                /// <summary>The chosen template in Content Service, or null.</summary>
                public Guid? TemplateId { get; init; }

                /// <summary>The chosen theme in Content Service, or null.</summary>
                public Guid? ThemeId { get; init; }

                /// <summary>All categories in the game.</summary>
                public required IReadOnlyList<CategoryReceiverDto> CategoryReceivers { get; init; }

                /// <summary>All items in the game. Each item points to its category with CategoryReceiverId.</summary>
                public required IReadOnlyList<CategoryItemDto> CategoryItems { get; init; }
        }
}
