using EditorService.Application.Interfaces.Repositories.Games;
using EditorService.Domain.Entities;
using EditorService.Domain.Entities.SortingGames;
using EditorService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;


namespace EditorService.Infrastructure.Repositories.Games
{
        /// <summary>
        /// Saves and loads games with EF Core. Implements <see cref="IGameRepository"/> from Application.
        /// </summary>
        public sealed class GameRepository : IGameRepository
        {
                private readonly EditorDbContext _dbContext;

                /// <summary>
                /// Initializes a new instance of the <see cref="GameRepository"/> class.
                /// </summary>
                /// <param name="dbContext">The Editor database.</param>
                public GameRepository(EditorDbContext dbContext)
                {
                        this._dbContext = dbContext;
                }

                /// <inheritdoc/>
                public async Task<GameEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
                {
                        // Looks up the game by its primary key. EF Core tracks it, so changes are saved later.
                        return await this._dbContext.Games.FindAsync(keyValues: [id], cancellationToken: cancellationToken);
                }

                /// <inheritdoc/>
                public async Task<SortingGameEntity?> GetSortingGameByIdAsync(Guid id, CancellationToken cancellationToken)
                {
                        // OfType only looks at sorting games, so a quiz with the same id gives null.
                        // Categories and items are owned by the game, so EF Core loads them automatically.
                        // EF Core tracks the game, so changes from the handler are saved later.
                        return await this._dbContext.Games
                                .OfType<SortingGameEntity>()
                                .Where(predicate: game => game.Id == id)
                                .FirstOrDefaultAsync(cancellationToken: cancellationToken);
                }

                /// <inheritdoc/>
                public Task AddAsync(GameEntity game, CancellationToken cancellationToken)
                {
                        // Marks the game as new. Nothing is written until SaveChangesAsync.
                        this._dbContext.Games.Add(entity: game);
                        return Task.CompletedTask;
                }

                /// <inheritdoc/>
                public Task UpdateAsync(GameEntity game, CancellationToken cancellationToken)
                {
                        // Marks the game as changed. Only needed for a game that EF Core does not already track.
                        this._dbContext.Games.Update(entity: game);
                        return Task.CompletedTask;
                }

                /// <inheritdoc/>
                public Task DeleteAsync(GameEntity game, CancellationToken cancellationToken)
                {
                        // Marks the game for deletion. Its receivers and items go with it, because they are owned.
                        this._dbContext.Games.Remove(entity: game);
                        return Task.CompletedTask;
                }

                /// <inheritdoc/>
                public async Task SaveChangesAsync(CancellationToken cancellationToken)
                {
                        // Writes everything to the database in one transaction.
                        await this._dbContext.SaveChangesAsync(cancellationToken: cancellationToken);
                }
        }
}
