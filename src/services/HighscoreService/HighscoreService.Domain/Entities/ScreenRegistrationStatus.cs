namespace HighscoreService.Domain.Entities
{
        /// <summary>
        /// Lifecycle status of an exhibition screen server enrolled with the central Hub.
        /// </summary>
        public enum ScreenRegistrationStatus
        {
                /// <summary>
                /// Newly enrolled screen awaiting administrator approval before it can sync or write to the Hub.
                /// </summary>
                Pending = 0,

                /// <summary>
                /// Approved screen authorized to push sync batches, pull deltas, and record scores on the Hub.
                /// </summary>
                Approved = 1,

                /// <summary>
                /// Revoked screen whose bearer credential is immediately rejected by the Hub.
                /// </summary>
                Revoked = 2
        }
}

