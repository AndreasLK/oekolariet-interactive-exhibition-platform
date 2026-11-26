using Asp.Versioning;
using EditorService.Api.Mapper;
using EditorService.Application.Interfaces.CommandHandlers.SortingGames.CategoryItems;
using EditorService.Application.Interfaces.CommandHandlers.SortingGames.CategoryRecievers;
using EditorService.Application.Interfaces.Queries.SortingGames;
using EditorService.Facade.DTOs.SortingGames;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using EditorService.Facade.DTOs.SortingGames.CategoryItems;
using EditorService.Facade.DTOs.SortingGames.CategoryRecievers;
using EditorService.Facade.Commands.SortingGames.CategoryItems;
using EditorService.Facade.Commands.SortingGames.CategoryRecievers;
using EditorService.Sdk.SortingGames.Requests.CategoryItems;
using EditorService.Sdk.SortingGames.Requests.CategoryRecievers;
using EditorService.Sdk.SortingGames.Responses.CategoryItems;
using EditorService.Sdk.SortingGames.Responses.CategoryRecievers;
using EditorService.Sdk.SortingGames.Responses;

namespace EditorService.Api.Controllers.V1
{
        /// <summary>
        /// Endpoints for the content of a sorting game: the categories (receivers) and the items that are sorted into them.
        /// Creating, renaming, publishing and deleting the game itself is done in <see cref="GamesController"/>.
        /// </summary>
        [ApiController]
        [ApiVersion(version: 1.0)]
        [Route(template: "api/v{version:apiVersion}/sorting-games/{gameId:guid}")]
        public sealed class SortingGamesController : EditorControllerBase
        {
                private readonly ISortingGameQueries _sortingGameQueries;
                private readonly IAddCategoryReceiverCommandHandler _addCategoryReceiver;
                private readonly IUpdateCategoryReceiverCommandHandler _updateCategoryReceiver;
                private readonly IRemoveCategoryReceiverCommandHandler _removeCategoryReceiver;
                private readonly IAddCategoryItemCommandHandler _addCategoryItem;
                private readonly IUpdateCategoryItemCommandHandler _updateCategoryItem;
                private readonly IRemoveCategoryItemCommandHandler _removeCategoryItem;

                /// <summary>
                /// Initializes a new instance of the <see cref="SortingGamesController"/> class.
                /// </summary>
                /// <param name="sortingGameQueries">Reads a sorting game with all its content.</param>
                /// <param name="addCategoryReceiver">Adds a category.</param>
                /// <param name="updateCategoryReceiver">Changes a category.</param>
                /// <param name="removeCategoryReceiver">Removes a category and its items.</param>
                /// <param name="addCategoryItem">Adds an item.</param>
                /// <param name="updateCategoryItem">Changes an item.</param>
                /// <param name="removeCategoryItem">Removes an item.</param>
                public SortingGamesController(
                        ISortingGameQueries sortingGameQueries,
                        IAddCategoryReceiverCommandHandler addCategoryReceiver,
                        IUpdateCategoryReceiverCommandHandler updateCategoryReceiver,
                        IRemoveCategoryReceiverCommandHandler removeCategoryReceiver,
                        IAddCategoryItemCommandHandler addCategoryItem,
                        IUpdateCategoryItemCommandHandler updateCategoryItem,
                        IRemoveCategoryItemCommandHandler removeCategoryItem)
                {
                        this._sortingGameQueries = sortingGameQueries;
                        this._addCategoryReceiver = addCategoryReceiver;
                        this._updateCategoryReceiver = updateCategoryReceiver;
                        this._removeCategoryReceiver = removeCategoryReceiver;
                        this._addCategoryItem = addCategoryItem;
                        this._updateCategoryItem = updateCategoryItem;
                        this._removeCategoryItem = removeCategoryItem;
                }

                // ---------- Sorting game ----------

                /// <summary>
                /// Gets a sorting game with all its categories and items.
                /// When an employee opens a sorting game in the editor, this endpoint loads everything the editor needs to show it.
                /// It only reads, so it goes through the queries and not through the domain.
                /// </summary>
                /// <remarks>
                /// 200 with the whole game. 404 when the game does not exist or is not a sorting game.
                /// </remarks>
                /// <param name="gameId">The unique identifier of the game.</param>
                /// <param name="cancellationToken">A token that cancels the operation.</param>
                /// <returns>The sorting game with its categories and items.</returns>
                [HttpGet(template: "")]
                [EndpointSummary(summary: "Henter et sorteringsspil med kategorier og items")]
                [ProducesResponseType(statusCode: StatusCodes.Status200OK)]
                [ProducesResponseType(statusCode: StatusCodes.Status404NotFound)]
                public async Task<ActionResult<SortingGameDetailsResponse>> GetSortingGameAsync(Guid gameId, CancellationToken cancellationToken)
                {
                        SortingGameDetailsDto? sortingGame = await this._sortingGameQueries.GetDetailsAsync(gameId: gameId, cancellationToken: cancellationToken);

                        // No sorting game with that id: 404. Otherwise: 200 with the whole game.
                        if (sortingGame is null)
                        {
                                return this.NotFound();
                        }

                        return this.Ok(value: SortingGameMapper.ToResponse(dto: sortingGame));
                }

                // ---------- Category receivers ----------

                /// <summary>
                /// Adds a category (receiver) to a sorting game.
                /// When an employee clicks "New category", e.g. "Plastic" or "Paper", this endpoint creates it.
                /// The color and image are optional, so a category can be created first and styled afterwards.
                /// </summary>
                /// <remarks>
                /// 201 with the new category. 400 when the name, color or image is not valid, or the game is not a sorting game.
                /// 404 when the game does not exist. 409 when the lock token is wrong or the game was saved by someone else in the meantime.
                /// </remarks>
                /// <param name="gameId">The unique identifier of the game.</param>
                /// <param name="lockToken">The edit lock token from the X-Lock-Token header.</param>
                /// <param name="request">The name, color and image of the new category.</param>
                /// <param name="cancellationToken">A token that cancels the operation.</param>
                /// <returns>The new category with its id.</returns>
                [HttpPost(template: "receivers")]
                [EndpointSummary(summary: "Tilføjer en kategori til et sorteringsspil")]
                [ProducesResponseType(statusCode: StatusCodes.Status201Created)]
                [ProducesResponseType(statusCode: StatusCodes.Status400BadRequest)]
                [ProducesResponseType(statusCode: StatusCodes.Status404NotFound)]
                [ProducesResponseType(statusCode: StatusCodes.Status409Conflict)]
                public async Task<ActionResult<CategoryReceiverResponse>> AddCategoryReceiverAsync(Guid gameId, [FromHeader(Name = "X-Lock-Token")] Guid lockToken, AddCategoryReceiverRequest request, CancellationToken cancellationToken)
                {
                        CategoryReceiverDto receiver = await this._addCategoryReceiver.HandleAsync(command: SortingGameMapper.ToCommand(request: request, gameId: gameId, lockToken: lockToken, actorId: this.ActorId), cancellationToken: cancellationToken);

                        // The new category has no endpoint of its own, so the Location header points to the whole sorting game.
                        return this.CreatedAtAction(
                                actionName: nameof(this.GetSortingGameAsync),
                                routeValues: new { gameId = gameId },
                                value: SortingGameMapper.ToResponse(dto: receiver));
                }

                /// <summary>
                /// Changes the name, color and image of a category.
                /// When an employee edits a category in the editor, this endpoint saves the change.
                /// </summary>
                /// <remarks>
                /// 204 when the category is changed. 400 when the values are not valid or the category does not exist in this game.
                /// 404 when the game does not exist. 409 when the lock token is wrong or the game was saved by someone else in the meantime.
                /// </remarks>
                /// <param name="gameId">The unique identifier of the game.</param>
                /// <param name="receiverId">The unique identifier of the category.</param>
                /// <param name="lockToken">The edit lock token from the X-Lock-Token header.</param>
                /// <param name="request">The new name, color and image.</param>
                /// <param name="cancellationToken">A token that cancels the operation.</param>
                /// <returns>No content when the category is changed.</returns>
                [HttpPut(template: "receivers/{receiverId:guid}")]
                [EndpointSummary(summary: "Opdaterer en kategori")]
                [ProducesResponseType(statusCode: StatusCodes.Status204NoContent)]
                [ProducesResponseType(statusCode: StatusCodes.Status400BadRequest)]
                [ProducesResponseType(statusCode: StatusCodes.Status404NotFound)]
                [ProducesResponseType(statusCode: StatusCodes.Status409Conflict)]
                public async Task<ActionResult> UpdateCategoryReceiverAsync(Guid gameId, Guid receiverId, [FromHeader(Name = "X-Lock-Token")] Guid lockToken, UpdateCategoryReceiverRequest request, CancellationToken cancellationToken)
                {
                        await this._updateCategoryReceiver.HandleAsync(command: SortingGameMapper.ToCommand(request: request, gameId: gameId, receiverId: receiverId, lockToken: lockToken, actorId: this.ActorId), cancellationToken: cancellationToken);
                        return this.NoContent();
                }

                /// <summary>
                /// Removes a category from a sorting game.
                /// All items in the category are removed with it, so no item is left without a category.
                /// </summary>
                /// <remarks>
                /// 204 when the category is removed. 400 when the category does not exist in this game.
                /// 404 when the game does not exist. 409 when the lock token is wrong or the game was saved by someone else in the meantime.
                /// </remarks>
                /// <param name="gameId">The unique identifier of the game.</param>
                /// <param name="receiverId">The unique identifier of the category.</param>
                /// <param name="lockToken">The edit lock token from the X-Lock-Token header.</param>
                /// <param name="cancellationToken">A token that cancels the operation.</param>
                /// <returns>No content when the category is removed.</returns>
                [HttpDelete(template: "receivers/{receiverId:guid}")]
                [EndpointSummary(summary: "Sletter en kategori og dens items")]
                [ProducesResponseType(statusCode: StatusCodes.Status204NoContent)]
                [ProducesResponseType(statusCode: StatusCodes.Status400BadRequest)]
                [ProducesResponseType(statusCode: StatusCodes.Status404NotFound)]
                [ProducesResponseType(statusCode: StatusCodes.Status409Conflict)]
                public async Task<ActionResult> RemoveCategoryReceiverAsync(Guid gameId, Guid receiverId, [FromHeader(Name = "X-Lock-Token")] Guid lockToken, CancellationToken cancellationToken)
                {
                        await this._removeCategoryReceiver.HandleAsync(command: new RemoveCategoryReceiverCommand(GameId: gameId, CategoryReceiverId: receiverId, LockToken: lockToken, ActorId: this.ActorId), cancellationToken: cancellationToken);
                        return this.NoContent();
                }

                // ---------- Category items ----------

                /// <summary>
                /// Adds an item to a category, e.g. "Milk carton" to "Paper".
                /// The visitor later drags the item to the category it belongs to, and sees the explanation afterwards.
                /// </summary>
                /// <remarks>
                /// 201 with the new item. 400 when the values are not valid, the category does not exist in this game, or the game is not a sorting game.
                /// 404 when the game does not exist. 409 when the lock token is wrong or the game was saved by someone else in the meantime.
                /// </remarks>
                /// <param name="gameId">The unique identifier of the game.</param>
                /// <param name="lockToken">The edit lock token from the X-Lock-Token header.</param>
                /// <param name="request">The name, explanation, image and category of the new item.</param>
                /// <param name="cancellationToken">A token that cancels the operation.</param>
                /// <returns>The new item with its id.</returns>
                [HttpPost(template: "items")]
                [EndpointSummary(summary: "Tilføjer et item til en kategori")]
                [ProducesResponseType(statusCode: StatusCodes.Status201Created)]
                [ProducesResponseType(statusCode: StatusCodes.Status400BadRequest)]
                [ProducesResponseType(statusCode: StatusCodes.Status404NotFound)]
                [ProducesResponseType(statusCode: StatusCodes.Status409Conflict)]
                public async Task<ActionResult<CategoryItemResponse>> AddCategoryItemAsync(Guid gameId, [FromHeader(Name = "X-Lock-Token")] Guid lockToken, AddCategoryItemRequest request, CancellationToken cancellationToken)
                {
                        CategoryItemDto item = await this._addCategoryItem.HandleAsync(command: SortingGameMapper.ToCommand(request: request, gameId: gameId, lockToken: lockToken, actorId: this.ActorId), cancellationToken: cancellationToken);

                        // The new item has no endpoint of its own, so the Location header points to the whole sorting game.
                        return this.CreatedAtAction(
                                actionName: nameof(this.GetSortingGameAsync),
                                routeValues: new { gameId = gameId },
                                value: SortingGameMapper.ToResponse(dto: item));
                }

                /// <summary>
                /// Changes the name, explanation and image of an item.
                /// The item stays in its category: an employee cannot move an item to another category.
                /// </summary>
                /// <remarks>
                /// 204 when the item is changed. 400 when the values are not valid or the item does not exist in this game.
                /// 404 when the game does not exist. 409 when the lock token is wrong or the game was saved by someone else in the meantime.
                /// </remarks>
                /// <param name="gameId">The unique identifier of the game.</param>
                /// <param name="itemId">The unique identifier of the item.</param>
                /// <param name="lockToken">The edit lock token from the X-Lock-Token header.</param>
                /// <param name="request">The new name, explanation and image.</param>
                /// <param name="cancellationToken">A token that cancels the operation.</param>
                /// <returns>No content when the item is changed.</returns>
                [HttpPut(template: "items/{itemId:guid}")]
                [EndpointSummary(summary: "Opdaterer et item")]
                [ProducesResponseType(statusCode: StatusCodes.Status204NoContent)]
                [ProducesResponseType(statusCode: StatusCodes.Status400BadRequest)]
                [ProducesResponseType(statusCode: StatusCodes.Status404NotFound)]
                [ProducesResponseType(statusCode: StatusCodes.Status409Conflict)]
                public async Task<ActionResult> UpdateCategoryItemAsync(Guid gameId, Guid itemId, [FromHeader(Name = "X-Lock-Token")] Guid lockToken, UpdateCategoryItemRequest request, CancellationToken cancellationToken)
                {
                        await this._updateCategoryItem.HandleAsync(command: SortingGameMapper.ToCommand(request: request, gameId: gameId, itemId: itemId, lockToken: lockToken, actorId: this.ActorId), cancellationToken: cancellationToken);
                        return this.NoContent();
                }

                /// <summary>
                /// Removes an item from a sorting game.
                /// </summary>
                /// <remarks>
                /// 204 when the item is removed. 400 when the item does not exist in this game.
                /// 404 when the game does not exist. 409 when the lock token is wrong or the game was saved by someone else in the meantime.
                /// </remarks>
                /// <param name="gameId">The unique identifier of the game.</param>
                /// <param name="itemId">The unique identifier of the item.</param>
                /// <param name="lockToken">The edit lock token from the X-Lock-Token header.</param>
                /// <param name="cancellationToken">A token that cancels the operation.</param>
                /// <returns>No content when the item is removed.</returns>
                [HttpDelete(template: "items/{itemId:guid}")]
                [EndpointSummary(summary: "Sletter et item")]
                [ProducesResponseType(statusCode: StatusCodes.Status204NoContent)]
                [ProducesResponseType(statusCode: StatusCodes.Status400BadRequest)]
                [ProducesResponseType(statusCode: StatusCodes.Status404NotFound)]
                [ProducesResponseType(statusCode: StatusCodes.Status409Conflict)]
                public async Task<ActionResult> RemoveCategoryItemAsync(Guid gameId, Guid itemId, [FromHeader(Name = "X-Lock-Token")] Guid lockToken, CancellationToken cancellationToken)
                {
                        await this._removeCategoryItem.HandleAsync(command: new RemoveCategoryItemCommand(GameId: gameId, CategoryItemId: itemId, LockToken: lockToken, ActorId: this.ActorId), cancellationToken: cancellationToken);
                        return this.NoContent();
                }
        }
}
