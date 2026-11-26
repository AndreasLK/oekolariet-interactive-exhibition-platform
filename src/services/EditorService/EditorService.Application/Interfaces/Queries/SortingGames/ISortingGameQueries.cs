using EditorService.Facade.DTOs.SortingGames;

namespace EditorService.Application.Interfaces.Queries.SortingGames
{
        /// <summary>
        /// Reads sorting games with all their content. Never changes anything.
        /// </summary>
        public interface ISortingGameQueries
        {
                /// <summary>
                /// Gets a sorting game with its categories and items.
                /// </summary>
                /// <param name="gameId">The id of the game.</param>
                /// <param name="cancellationToken">Stops the call if the request is cancelled.</param>
                /// <returns>The sorting game, or <see langword="null"/> if it does not exist or is not a sorting game.</returns>
                public Task<SortingGameDetailsDto?> GetDetailsAsync(Guid gameId, CancellationToken cancellationToken);
        }
}
