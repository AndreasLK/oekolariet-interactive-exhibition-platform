using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Application.Exceptions
{
        /// <summary>
        /// Thrown when a game with the given id does not exist. Mapped to 404.
        /// </summary>
        public sealed class GameNotFoundException : Exception
        {
                /// <summary>
                /// Initializes a new instance of the <see cref="GameNotFoundException"/> class.
                /// </summary>
                /// <param name="gameId">The id that was not found.</param>
                public GameNotFoundException(Guid gameId)
                        : base(message: $"Game with id {gameId} was not found.")
                {
                }
        }
}
