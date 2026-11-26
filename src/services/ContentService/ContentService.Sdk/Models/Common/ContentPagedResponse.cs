namespace ContentService.Sdk.Models;

/// <summary>
/// Represents a single page of ContentService query results along with total item and page counts.
/// </summary>
/// <typeparam name="T">The item type contained in the page.</typeparam>
public sealed record class ContentPagedResponse<T>
{
        /// <summary>
        /// Gets the read-only list of items belonging to the current page.
        /// </summary>
        public required IReadOnlyList<T> Items { get; init; }

        /// <summary>
        /// Gets the 1-based page number of this result slice.
        /// </summary>
        public required int Page { get; init; }

        /// <summary>
        /// Gets the maximum number of items requested per page.
        /// </summary>
        public required int PageSize { get; init; }

        /// <summary>
        /// Gets the total number of matching items across all pages.
        /// </summary>
        public required int TotalCount { get; init; }

        /// <summary>
        /// Gets the total number of pages available given <see cref="TotalCount"/> and <see cref="PageSize"/>.
        /// </summary>
        public int TotalPages
        {
                get
                {
                        if (this.TotalCount <= 0 || this.PageSize <= 0)
                        {
                                return 0;
                        }

                        // Uses 64-bit integer division so adding (PageSize - 1) near int.MaxValue cannot overflow.
                        return (int)(((long)this.TotalCount + this.PageSize - 1L) / this.PageSize);
                }
        }
}

