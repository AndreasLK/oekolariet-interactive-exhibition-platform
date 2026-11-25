using EditorService.Api.Mapper;
using EditorService.Application.Interfaces.CommandHandlers.Games;
using EditorService.Facade.DTOs.Games;
using EditorService.Sdk.Games.Requests;
using EditorService.Sdk.Games.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EditorService.Api.Controllers.V1
{
        /// <summary>
        /// Endpoints for creating and editing games.
        /// </summary>
        [ApiController]
        [Route(template: "api/v1/games")]
        public sealed class GamesController : ControllerBase
        {
                private readonly ICreateSortingGameCommandHandler _createSortingGame;

                /// <summary>
                /// Initializes a new instance of the <see cref="GamesController"/> class.
                /// </summary>
                /// <param name="createSortingGame">Creates sorting games.</param>
                public GamesController(ICreateSortingGameCommandHandler createSortingGame)
                {
                        this._createSortingGame = createSortingGame;
                }

                [HttpPost(template: "sorting")]
                [EndpointSummary(summary: "Opretter et nyt sorteringsspil som kladde")]
                [ProducesResponseType(statusCode: StatusCodes.Status201Created)]
                [ProducesResponseType(statusCode: StatusCodes.Status400BadRequest)]
                public async Task<ActionResult<GameResponse>> CreateSortingGameAsync(CreateSortingGameRequest request, CancellationToken cancellationToken)
                {
                        GameDto game = await this._createSortingGame.HandleAsync(command: GameMapper.ToCommand(request: request, actorId: this.ActorId), cancellationToken: cancellationToken);
                        return this.Created(uri: $"/api/v1/games/{game.Id}", value: GameMapper.ToResponse(dto: game));
                }

                // TODO: read the employee from the JWT when login is added.
                private string ActorId => this.User.Identity?.Name ?? "unknown";
        }
}
