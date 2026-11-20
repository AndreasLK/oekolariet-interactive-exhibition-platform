using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Domain.Enums
{
        /// <summary>
        /// Represents the kind of game an employee can build in the editor.
        /// </summary>
        public enum GameType
        {
                /// <summary>
                /// A drag-and-drop game where waste items are sorted into bins.
                /// </summary>
                SortingGame,

                /// <summary>
                /// A quiz with questions and answer options.
                /// </summary>
                Quiz
        }
}
