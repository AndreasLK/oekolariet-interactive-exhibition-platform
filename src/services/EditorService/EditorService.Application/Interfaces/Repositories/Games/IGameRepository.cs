using EditorService.Domain.Entities;
using EditorService.Domain.Entities.SortingGames;
using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Application.Interfaces.Repositories.Games
{
        /// <summary>
        /// Saves and loads games. Implemented in Infrastructure with EF Core.
        /// </summary>
        public interface IGameRepository
        {
                /// <summary>Gets a game by its id, or <see langword="null"/> if it does not exist.</summary>
                /// <param name="id">The id of the game.</param>
                /// <param name="cancellationToken">Stops the call if the request is cancelled.</param>
                /// <returns>The game, or <see langword="null"/>.</returns>
                public Task<GameEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

                /// <summary>Gets a sorting game by its id, with all its categories and items.</summary>
                /// <param name="id">The id of the game.</param>
                /// <param name="cancellationToken">Stops the call if the request is cancelled.</param>
                /// <returns>The sorting game, or <see langword="null"/> if it does not exist or is not a sorting game.</returns>
                public Task<SortingGameEntity?> GetSortingGameByIdAsync(Guid id, CancellationToken cancellationToken);

                /// <summary>Saves a new game.</summary>
                /// <param name="game">The game to save.</param>
                /// <param name="cancellationToken">Stops the call if the request is cancelled.</param>
                /// <returns>A task that finishes when the game is saved.</returns>
                public Task AddAsync(GameEntity game, CancellationToken cancellationToken);

                /// <summary>Saves changes to an existing game.</summary>
                /// <param name="game">The changed game.</param>
                /// <param name="cancellationToken">Stops the call if the request is cancelled.</param>
                /// <returns>A task that finishes when the changes are saved.</returns>
                public Task UpdateAsync(GameEntity game, CancellationToken cancellationToken);

                /// <summary>Deletes a game.</summary>
                /// <param name="game">The game to delete.</param>
                /// <param name="cancellationToken">Stops the call if the request is cancelled.</param>
                /// <returns>A task that finishes when the game is deleted.</returns>
                public Task DeleteAsync(GameEntity game, CancellationToken cancellationToken);

                /// <summary>Writes all added and changed games to the database in one transaction.</summary>
                /// <param name="cancellationToken">Stops the call if the request is cancelled.</param>
                /// <returns>A task that finishes when everything is saved.</returns>
                public Task SaveChangesAsync(CancellationToken cancellationToken);
        }
}
