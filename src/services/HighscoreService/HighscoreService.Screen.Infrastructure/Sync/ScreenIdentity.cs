namespace HighscoreService.Screen.Infrastructure.Sync
{
        /// <summary>
        /// Represents the persisted screen credential pair.
        /// </summary>
        /// <param name="ScreenId">Unique screen identifier.</param>
        /// <param name="Secret">Base64-encoded 32-byte secret.</param>
        public sealed record ScreenIdentity(Guid ScreenId, string Secret)
        {
                /// <summary>
                /// Formats the credential as a <c>scr_&lt;id&gt;.&lt;secret&gt;</c> bearer token.
                /// </summary>
                /// <returns>Formatted bearer token string.</returns>
                public string ToBearerToken()
                {
                        // Prefix with 'scr_' so Hub authentication routes this token to screen validation.
                        return $"scr_{this.ScreenId}.{this.Secret}";
                }
        }
}

