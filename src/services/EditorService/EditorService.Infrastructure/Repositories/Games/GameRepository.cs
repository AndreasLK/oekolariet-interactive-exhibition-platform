using EditorService.Application.Interfaces.Repositories.Games;
using EditorService.Domain.Entities;
using EditorService.Infrastructure.Persistence;


namespace EditorService.Infrastructure.Repositories.Games
{
        public sealed class GameRepository : IGameRepository
        {

                private readonly EditorDbContext _dbContext;

                public GameRepository(EditorDbContext dbContext)
                {
                        this._dbContext = dbContext;
                }
                /// <summary>Gets a game by its id, or <see langword="null"/> if it does not exist.</summary>
                /// <param name="id">The id of the game.</param>
                /// <param name="cancellationToken">Stops the call if the request is cancelled.</param>
                /// <returns>The game, or <see langword="null"/>.</returns>
                public async Task<GameEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
                {
                        // Implementation for getting a game by ID
                        return await this._dbContext.Games.FindAsync(id, cancellationToken);
                }

                /// <summary>Saves a new game.</summary>
                /// <param name="game">The game to save.</param>
                /// <param name="cancellationToken">Stops the call if the request is cancelled.</param>
                /// <returns>A task that finishes when the game is saved.</returns>
                public async Task AddAsync(GameEntity game, CancellationToken cancellationToken)
                {
                        // Implementation for adding a game
                        this._dbContext.Games.Add(game);
                }

                /// <summary>Saves changes to an existing game.</summary>
                /// <param name="game">The changed game.</param>
                /// <param name="cancellationToken">Stops the call if the request is cancelled.</param>
                /// <returns>A task that finishes when the changes are saved.</returns>
                public async Task UpdateAsync(GameEntity game, CancellationToken cancellationToken)
                {
                        // Implementation for updating a game
                        this._dbContext.Games.Update(game);
                }

                /// <summary>Deletes a game.</summary>
                /// <param name="game">The game to delete.</param>
                /// <param name="cancellationToken">Stops the call if the request is cancelled.</param>
                /// <returns>A task that finishes when the game is deleted.</returns>
                public async Task DeleteAsync(GameEntity game, CancellationToken cancellationToken)
                {
                        // Implementation for deleting a game
                        this._dbContext.Games.Remove(game);
                }

                public async Task SaveChangesAsync(CancellationToken cancellationToken)
                {
                        // Writes everything to the database in one transaction.
                        await this._dbContext.SaveChangesAsync(cancellationToken: cancellationToken);
                }
        }
}
