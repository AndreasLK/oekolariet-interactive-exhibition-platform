using EditorService.Application.Interfaces.Queries.Games;
using EditorService.Domain.Entities;
using EditorService.Facade.DTOs.Games;
using EditorService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EditorService.Infrastructure.Queries.Games
{
        /// <summary>
        /// Reads games from the database and returns them as DTOs. Never changes anything.
        /// </summary>
        public sealed class GameQueries : IGameQueries
        {
                private readonly EditorDbContext _dbContext;

                /// <summary>
                /// Initializes a new instance of the <see cref="GameQueries"/> class.
                /// </summary>
                /// <param name="dbContext">The Editor database.</param>
                public GameQueries(EditorDbContext dbContext)
                {
                        this._dbContext = dbContext;
                }

                /// <inheritdoc/>
                public async Task<GameDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
                {
                        // AsNoTracking: we only read, so EF Core does not need to watch for changes.
                        GameEntity? game = await this._dbContext.Games
                                .AsNoTracking()
                                .FirstOrDefaultAsync(predicate: g => g.Id == id, cancellationToken: cancellationToken);

                        // No game with that id: return null, so the controller can answer 404.
                        if (game is null)
                        {
                                return null;
                        }

                        return ToDto(game: game);
                }

                /// <inheritdoc/>
                public async Task<IReadOnlyList<GameDto>> GetAllAsync(CancellationToken cancellationToken)
                {
                        List<GameEntity> games = await this._dbContext.Games
                                .AsNoTracking()
                                .OrderBy(keySelector: g => g.Title)
                                .ToListAsync(cancellationToken: cancellationToken);

                        return games.Select(selector: ToDto).ToList();
                }

                // Turns a game into the DTO the editor needs.
                private static GameDto ToDto(GameEntity game)
                {
                        return new GameDto
                        {
                                Id = game.Id,
                                Title = game.Title,
                                Type = game.Type.ToString(),
                                Status = game.Status.ToString()
                        };
                }
        }
}
