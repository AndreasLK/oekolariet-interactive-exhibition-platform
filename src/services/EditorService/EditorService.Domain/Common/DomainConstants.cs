using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Domain.Common
{
        public static class DomainConstants
        {
                /// <summary>
                /// Maximum allowed character length for an actor identifier.
                /// </summary>
                public const int MAX_ACTOR_ID_LENGTH = 128;

                /// <summary>
                /// Maximum allowed character length for a game title.
                /// </summary>
                public const int MAX_GAME_TITLE_LENGTH = 150;

                /// <summary>
                /// Maximum allowed character length for the name of the person holding an edit lock.
                /// </summary>
                public const int MAX_LOCK_HOLDER_NAME_LENGTH = 100;

                /// <summary>
                /// Maximum allowed character length for the name of the device holding an edit lock.
                /// </summary>
                public const int MAX_LOCK_DEVICE_NAME_LENGTH = 100;

                /// <summary>
                /// Number of minutes an edit lock stays valid without being renewed.
                /// </summary>
                public const int EDIT_LOCK_DURATION_MINUTES = 15;

                /// <summary>
                /// Maximum allowed character length for a bin name.
                /// </summary>
                public const int MAX_BIN_NAME_LENGTH = 100;

                /// <summary>Maximum allowed character length for a waste item name.</summary>
                public const int MAX_WASTE_ITEM_NAME_LENGTH = 100;

                /// <summary>Maximum allowed character length for an explanation shown to the player.</summary>
                public const int MAX_EXPLANATION_LENGTH = 500;

                /// <summary>Maximum allowed character length for a category receiver name.</summary>
                public const int MAX_CATEGORY_RECEIVER_NAME_LENGTH = 50;

                /// <summary>Maximum allowed character length for a color string, e.g. "#1D9E75".</summary>
                public const int MAX_COLOR_LENGTH = 20;
                /// <summary>
                /// Maximum allowed character length for a category item name.
                /// </summary>
                public const int MAX_CATEGORY_ITEM_NAME_LENGTH = 100;
                /// <summary> Maximum allowed character length for an explanation shown to the player.</summary>

        }
}
