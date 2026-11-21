using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Facade.DTOs.Games
{
        /// <summary>
        /// The basic information about a game, sent back after it is created or changed.
        /// </summary>
        public sealed record GameDto
        {
                /// <summary>Gets the unique identifier of the game.</summary>
                public required Guid Id { get; init; }

                /// <summary>Gets the title of the game.</summary>
                public required string Title { get; init; }

                /// <summary>Gets the kind of game, e.g. "SortingGame".</summary>
                public required string Type { get; init; }

                /// <summary>Gets the status of the game, e.g. "Draft".</summary>
                public required string Status { get; init; }
        }
}
