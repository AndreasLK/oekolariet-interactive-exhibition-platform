using EditorService.Application.Interfaces.Queries.SortingGames;
using EditorService.Domain.Entities.SortingGames;
using EditorService.Facade.DTOs.SortingGames;
using EditorService.Facade.DTOs.SortingGames.CategoryItems;
using EditorService.Facade.DTOs.SortingGames.CategoryRecievers;
using EditorService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EditorService.Infrastructure.Queries.SortingGames
{
        /// <summary>
        /// Reads sorting games with all their content and returns them as DTOs. Never changes anything.
        /// </summary>
        public sealed class SortingGameQueries : ISortingGameQueries
        {
                private readonly EditorDbContext _dbContext;

                /// <summary>
                /// Initializes a new instance of the <see cref="SortingGameQueries"/> class.
                /// </summary>
                /// <param name="dbContext">The Editor database.</param>
                public SortingGameQueries(EditorDbContext dbContext)
                {
                        this._dbContext = dbContext;
                }

                /// <inheritdoc/>
                public async Task<SortingGameDetailsDto?> GetDetailsAsync(Guid gameId, CancellationToken cancellationToken)
                {
                        // AsNoTracking: we only read. OfType: only sorting games. Categories and items are owned, so they come with it.
                        SortingGameEntity? game = await this._dbContext.Games
                                .AsNoTracking()
                                .OfType<SortingGameEntity>()
                                .FirstOrDefaultAsync(predicate: sortingGame => sortingGame.Id == gameId, cancellationToken: cancellationToken);

                        // No sorting game with that id: return null, so the controller can answer 404.
                        if (game is null)
                        {
                                return null;
                        }

                        return new SortingGameDetailsDto
                        {
                                Id = game.Id,
                                Title = game.Title,
                                Status = game.Status.ToString(),
                                TemplateId = game.TemplateId,
                                ThemeId = game.ThemeId,
                                CategoryReceivers = game.CategoryReceivers
                                        .Select(selector: receiver => new CategoryReceiverDto
                                        {
                                                Id = receiver.Id,
                                                Name = receiver.Name,
                                                Color = receiver.Color,
                                                ImageId = receiver.ImageId
                                        })
                                        .ToList(),
                                CategoryItems = game.CategoryItems
                                        .Select(selector: item => new CategoryItemDto
                                        {
                                                Id = item.Id,
                                                Name = item.Name,
                                                Explanation = item.Explanation,
                                                ImageId = item.ImageId,
                                                CategoryReceiverId = item.CategoryReceiverId
                                        })
                                        .ToList()
                        };
                }
        }
}
