using EditorService.Api.Mapper;
using EditorService.Application.Interfaces.CommandHandlers.Games;
using EditorService.Facade.DTOs.Games;
using EditorService.Sdk.Games.Requests;
using EditorService.Sdk.Games.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using EditorService.Application.Interfaces.Queries.Games;
using EditorService.Facade.Commands.Games;

namespace EditorService.Api.Controllers.V1
{
        /// <summary>
        /// Endpoints for creating and editing games.
        ///
        /// </summary>
        [ApiController]
        // API version (the contract): only changes on a breaking change, then a new V2 controller is added next to this one.
        // The service version (e.g. 0.1.0) is set in <Version> in EditorService.Api.csproj and changes with every release.
        [ApiVersion(version: 1.0)]
        [Route(template: "api/v{version:apiVersion}/games")]
        public sealed class GamesController : EditorControllerBase
        {
                private readonly ICreateSortingGameCommandHandler _createSortingGame;
                private readonly IRenameGameCommandHandler _renameGame;
                private readonly IUpdateGameTemplateCommandHandler _updateGameTemplate;
                private readonly IUpdateGameThemeCommandHandler _updateGameTheme;
                private readonly IPublishGameCommandHandler _publishGame;
                private readonly IDeleteGameCommandHandler _deleteGame;
                private readonly IGameQueries _gameQueries;


                /// <summary>
                /// Initializes a new instance of the <see cref="GamesController"/> class.
                /// </summary>
                /// <param name="createSortingGame">Creates sorting games.</param>
                /// <param name="renameGame">Renames games.</param>
                /// <param name="updateGameTemplate">Updates game templates.</param>
                /// <param name="updateGameTheme">Updates game themes.</param>
                /// <param name="publishGame">Publishes games.</param>
                /// <param name="deleteGame">Deletes games.</param>
                /// <param name="gameQueries">Provides queries for retrieving game information.</param>
                public GamesController(ICreateSortingGameCommandHandler createSortingGame,
                                       IRenameGameCommandHandler renameGame, IUpdateGameTemplateCommandHandler updateGameTemplate,
                                       IUpdateGameThemeCommandHandler updateGameTheme, IPublishGameCommandHandler publishGame,
                                       IDeleteGameCommandHandler deleteGame, IGameQueries gameQueries)
                {
                        this._createSortingGame = createSortingGame;
                        this._renameGame = renameGame;
                        this._updateGameTemplate = updateGameTemplate;
                        this._updateGameTheme = updateGameTheme;
                        this._publishGame = publishGame;
                        this._deleteGame = deleteGame;
                        this._gameQueries = gameQueries;
                }

                [HttpGet(template: "")]
                [EndpointSummary(summary: "Henter alle spil")]
                [ProducesResponseType(statusCode: StatusCodes.Status200OK)]
                public async Task<ActionResult<IEnumerable<GameResponse>>> GetAllGamesAsync(CancellationToken cancellationToken)
                {
                        IReadOnlyList<GameDto> games = await this._gameQueries.GetAllAsync(cancellationToken: cancellationToken);
                        return this.Ok(value: games.Select(selector: GameMapper.ToResponse));
                }


                /// <summary>
                /// Gets a game by its identifier.
                /// When an employee opens a game in the editor, this endpoint loads it.
                /// It only reads, so it goes through the queries and not through the domain.
                /// </summary>
                /// <remarks>
                /// 200 when the game is found. 404 when the game does not exist.
                /// </remarks>
                /// <param name="gameId">The unique identifier of the game.</param>
                /// <param name="cancellationToken">A token that cancels the operation.</param>
                /// <returns>The game when found; otherwise a not-found response.</returns>
                [HttpGet(template: "{gameId:guid}")]
                [EndpointSummary(summary: "Henter et spil")]
                [ProducesResponseType(statusCode: StatusCodes.Status200OK)]
                [ProducesResponseType(statusCode: StatusCodes.Status404NotFound)]
                public async Task<ActionResult<GameResponse>> GetGameAsync(Guid gameId, CancellationToken cancellationToken)
                {
                        GameDto? game = await this._gameQueries.GetByIdAsync(id: gameId, cancellationToken: cancellationToken);

                        // No game with that id: 404. Otherwise: 200 with the game.
                        return game is null
                                ? this.NotFound()
                                : this.Ok(value: GameMapper.ToResponse(dto: game));
                }

                /// <summary>
                /// Creates a new sorting game as a draft.
                ///When an employee clicks "New sorting game",
                ///CreateSortingGame only covers the very first moment: the game comes into existence,
                ///so there is something to work on. Everything else (receivers, items, images, theme, publishing)
                ///is handled by its own handlers afterwards.
                /// </summary>
                /// <param name="request">The request containing the sorting game's creation details.</param>
                /// <param name="cancellationToken">The token that cancels the operation.</param>
                /// <returns>The created sorting game.</returns>
                [HttpPost(template: "sorting")]
                [EndpointSummary(summary: "Opretter et nyt sorteringsspil som kladde")]
                [ProducesResponseType(statusCode: StatusCodes.Status201Created)]
                [ProducesResponseType(statusCode: StatusCodes.Status400BadRequest)]
                public async Task<ActionResult<GameResponse>> CreateSortingGameAsync(CreateSortingGameRequest request, CancellationToken cancellationToken)
                {
                        GameDto game = await this._createSortingGame.HandleAsync(command: GameMapper.ToCommand(request: request, actorId: this.ActorId), cancellationToken: cancellationToken);

                        // Builds the Location header from the route of GetGameAsync, so the URL follows the API version.
                        return this.CreatedAtAction(
                                actionName: nameof(this.GetGameAsync),
                                routeValues: new { gameId = game.Id },
                                value: GameMapper.ToResponse(dto: game));
                }

                /// <summary>
                /// Changes template requests to a game. The game must be in draft, and the actor must have the edit lock.
                /// </summary>
                /// <param name="gameId">Unikt id for det spil, der skal opdateres.</param>
                /// <param name="request">De opdaterede oplysninger for spillet.</param>
                /// <param name="cancellationToken">Token der annullerer operationen.</param>
                /// <returns>Spillet med de opdaterede oplysninger eller en status, der angiver at spillet ikke blev fundet.</returns>
                [HttpPut(template: "{gameId:guid}/title")]
                [EndpointSummary(summary: "Opdaterer et eksisterende spil")]
                [ProducesResponseType(statusCode: StatusCodes.Status200OK)]
                [ProducesResponseType(statusCode: StatusCodes.Status400BadRequest)]
                [ProducesResponseType(statusCode: StatusCodes.Status404NotFound)]
                [ProducesResponseType(statusCode: StatusCodes.Status409Conflict)]
                [ProducesResponseType(statusCode: StatusCodes.Status423Locked)]
                public async Task<ActionResult<GameResponse>> RenameGameAsync(Guid gameId, [FromHeader(Name = "X-Lock-Token")] Guid lockToken, RenameGameRequest request, CancellationToken cancellationToken)
                {
                        GameDto game = await this._renameGame.HandleAsync(command: GameMapper.ToCommand(request: request, gameId: gameId, lockToken: lockToken, actorId: this.ActorId), cancellationToken: cancellationToken);
                        return this.Ok(value: GameMapper.ToResponse(dto: game));
                }

                /// <summary>
                /// Changes the template of a game.
                /// When an employee picks a layout for the game, this endpoint saves the choice.
                /// The game must be in draft, and the employee must hold the edit lock,
                /// so two people cannot change the same game at the same time.
                /// Nothing is sent back, because the frontend already knows which template it chose.
                /// </summary>
                /// <remarks>
                /// 204 when the template is changed. 400 when the template id is not valid.
                /// 404 when the game does not exist. 409 when the lock token is wrong or the game was saved by someone else in the meantime.
                /// 423 when another employee holds the lock.
                /// </remarks>
                /// <param name="gameId">The unique identifier of the game.</param>
                /// <param name="lockToken">The edit lock token from the X-Lock-Token header.</param>
                /// <param name="request">The new template.</param>
                /// <param name="cancellationToken">A token that cancels the operation.</param>
                /// <returns>No content when the template is changed.</returns>
                [HttpPut(template: "{gameId:guid}/template")]
                [EndpointSummary(summary: "Opdaterer et eksisterende spil med en ny skabelon")]
                [ProducesResponseType(statusCode: StatusCodes.Status204NoContent)]
                [ProducesResponseType(statusCode: StatusCodes.Status400BadRequest)]
                [ProducesResponseType(statusCode: StatusCodes.Status404NotFound)]
                [ProducesResponseType(statusCode: StatusCodes.Status409Conflict)]
                [ProducesResponseType(statusCode: StatusCodes.Status423Locked)]
                public async Task<ActionResult> UpdateGameTemplateAsync(Guid gameId, [FromHeader(Name = "X-Lock-Token")] Guid lockToken, [FromBody] UpdateGameTemplateRequest request, CancellationToken cancellationToken)
                {
                        await this._updateGameTemplate.HandleAsync(command: GameMapper.ToCommand(request: request, gameId: gameId, lockToken: lockToken, actorId: this.ActorId), cancellationToken: cancellationToken);
                        return this.NoContent();
                }

                /// <summary>
                /// Changes the theme of a game.
                /// When an employee picks colors and look for the game, this endpoint saves the choice.
                /// The game must be in draft, and the employee must hold the edit lock,
                /// so two people cannot change the same game at the same time.
                /// Nothing is sent back, because the frontend already knows which theme it chose.
                /// </summary>
                /// <remarks>
                /// 204 when the theme is changed. 400 when the theme id is not valid.
                /// 404 when the game does not exist. 409 when the lock token is wrong or the game was saved by someone else in the meantime.
                /// 423 when another employee holds the lock.
                /// </remarks>
                /// <param name="gameId">The unique identifier of the game.</param>
                /// <param name="lockToken">The edit lock token from the X-Lock-Token header.</param>
                /// <param name="request">The new theme.</param>
                /// <param name="cancellationToken">A token that cancels the operation.</param>
                /// <returns>No content when the theme is changed.</returns>
                [HttpPut(template: "{gameId:guid}/theme")]
                [EndpointSummary(summary: "Opdaterer et eksisterende spil med et nyt tema")]
                [ProducesResponseType(statusCode: StatusCodes.Status204NoContent)]
                [ProducesResponseType(statusCode: StatusCodes.Status404NotFound)]
                [ProducesResponseType(statusCode: StatusCodes.Status409Conflict)]
                public async Task<ActionResult> UpdateGameThemeAsync(Guid gameId, [FromHeader(Name = "X-Lock-Token")] Guid lockToken, [FromBody] UpdateGameThemeRequest request, CancellationToken cancellationToken)
                {
                        await this._updateGameTheme.HandleAsync(command: GameMapper.ToCommand(request: request, gameId: gameId, lockToken: lockToken, actorId: this.ActorId), cancellationToken: cancellationToken);
                        return this.NoContent();
                }

                /// <summary>
                /// Publishes a game, so it can be shown in the exhibition.
                /// When an employee clicks "Publish", the domain first checks that the game is ready:
                /// it must have a template, at least one category receiver and at least one category item.
                /// The employee must hold the edit lock, because publishing changes the game.
                /// The game is sent back, because its status changes from Draft to Published
                /// and the frontend must show the new status right away.
                /// </summary>
                /// <remarks>
                /// 200 when the game is published. 400 when the game is not ready to be published.
                /// 404 when the game does not exist. 409 when the lock token is wrong or the game was saved by someone else in the meantime.
                /// 423 when another employee holds the lock.
                /// </remarks>
                /// <param name="gameId">The unique identifier of the game to publish.</param>
                /// <param name="lockToken">The edit lock token from the X-Lock-Token header.</param>
                /// <param name="cancellationToken">A token that cancels the operation.</param>
                /// <returns>The published game.</returns>
                [HttpPost(template: "{gameId:guid}/publish")]
                [EndpointSummary(summary: "Publicerer et eksisterende spil")]
                [ProducesResponseType(statusCode: StatusCodes.Status200OK)]
                [ProducesResponseType(statusCode: StatusCodes.Status400BadRequest)]
                [ProducesResponseType(statusCode: StatusCodes.Status404NotFound)]
                [ProducesResponseType(statusCode: StatusCodes.Status409Conflict)]
                [ProducesResponseType(statusCode: StatusCodes.Status423Locked)]
                public async Task<ActionResult<GameResponse>> PublishGameAsync(Guid gameId, [FromHeader(Name = "X-Lock-Token")] Guid lockToken, CancellationToken cancellationToken)
                {
                        GameDto game = await this._publishGame.HandleAsync(command: new PublishGameCommand(GameId: gameId, LockToken: lockToken, ActorId: this.ActorId), cancellationToken: cancellationToken);
                        return this.Ok(value: GameMapper.ToResponse(dto: game));
                }

                /// <summary>
                /// Deletes a game.
                /// When an employee removes a game they no longer need, this endpoint deletes it with all its receivers and items.
                /// The employee must hold the edit lock, so a game cannot be deleted while a colleague is working on it.
                /// The actor id is passed on, so we know who deleted the game.
                /// Nothing is sent back, because the game no longer exists.
                /// </summary>
                /// <remarks>
                /// 204 when the game is deleted. 404 when the game does not exist.
                /// 409 when the lock token is wrong. 423 when another employee holds the lock.
                /// </remarks>
                /// <param name="gameId">The unique identifier of the game to delete.</param>
                /// <param name="lockToken">The edit lock token from the X-Lock-Token header.</param>
                /// <param name="cancellationToken">A token that cancels the operation.</param>
                /// <returns>No content when the game is deleted.</returns>
                [HttpDelete(template: "{gameId:guid}")]
                [EndpointSummary(summary: "Sletter et eksisterende spil")]
                [ProducesResponseType(statusCode: StatusCodes.Status204NoContent)]
                [ProducesResponseType(statusCode: StatusCodes.Status404NotFound)]
                [ProducesResponseType(statusCode: StatusCodes.Status409Conflict)]
                [ProducesResponseType(statusCode: StatusCodes.Status423Locked)]
                public async Task<ActionResult> DeleteGameAsync(Guid gameId, [FromHeader(Name = "X-Lock-Token")] Guid lockToken, CancellationToken cancellationToken)
                {
                        await this._deleteGame.HandleAsync(command: new DeleteGameCommand(GameId: gameId, LockToken: lockToken, ActorId: this.ActorId), cancellationToken: cancellationToken);
                        return this.NoContent();
                }
        }
}
