namespace HighscoreService.Facade.Dtos
{
        /// <summary>
        /// Generic paged DTO returned by facade query methods.
        /// </summary>
        /// <typeparam name="T">The item DTO type.</typeparam>
        public sealed record PagedDto<T>(
                IReadOnlyList<T> Items,
                int TotalCount,
                int Page,
                int PageSize);
}

