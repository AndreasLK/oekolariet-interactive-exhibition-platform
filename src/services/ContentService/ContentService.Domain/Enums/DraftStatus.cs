namespace ContentService.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of a content draft.
/// </summary>
public enum DraftStatus
{
        /// <summary>
        /// The draft is actively being authored and is not yet published.
        /// </summary>
        Draft = 0,

        /// <summary>
        /// The draft has been published for exhibition display.
        /// </summary>
        Published = 1,

        /// <summary>
        /// The draft has been archived and is no longer active.
        /// </summary>
        Archived = 2
}
