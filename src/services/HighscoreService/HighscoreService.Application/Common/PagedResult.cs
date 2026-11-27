namespace HighscoreService.Application.Common
{
        /// <summary>
        /// Generic paged result wrapper returned by query use cases.
        /// </summary>
        /// <typeparam name="T">The item type.</typeparam>
        public sealed record PagedResult<T>(
                IReadOnlyList<T> Items,
                int TotalCount,
                int Page,
                int PageSize);
}

