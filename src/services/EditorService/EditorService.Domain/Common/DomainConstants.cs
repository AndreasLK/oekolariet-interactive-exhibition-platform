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
        }
}
