namespace ContentService.Facade.Common;

/// <summary>
/// Centralizes default pagination and facade boundary constants so API endpoints remain decoupled from Application internals.
/// </summary>
public static class FacadeConstants
{
        /// <summary>
        /// Default 1-based page index applied when callers omit explicit pagination parameters.
        /// </summary>
        public const int DEFAULT_PAGE = 1;

        /// <summary>
        /// Default number of items returned per page when callers omit an explicit page size.
        /// </summary>
        public const int DEFAULT_PAGE_SIZE = 25;
}

