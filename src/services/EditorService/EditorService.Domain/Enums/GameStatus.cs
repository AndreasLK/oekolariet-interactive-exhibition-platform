using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Domain.Enums
{
        public enum GameStatus
        {
                /// <summary>
                /// The game has changes that are not yet published to any screen.
                /// </summary>
                Draft,

                /// <summary>
                /// The current state of the game is identical to what was last published.
                /// </summary>
                Published
        }
}
