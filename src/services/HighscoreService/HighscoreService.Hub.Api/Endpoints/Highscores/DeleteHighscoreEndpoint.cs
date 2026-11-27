using HighscoreService.Application.Interfaces.CommandHandlers;
using HighscoreService.Facade.Commands;
using HighscoreService.Hub.Api.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace HighscoreService.Hub.Api.Endpoints.Highscores
{
        /// <summary>
        /// Maps soft-delete endpoints for single highscores and deleted EditorService games on the Hub.
        /// </summary>
        public static class DeleteHighscoreEndpoint
        {
                /// <summary>
                /// Registers <c>DELETE /api/v1/highscores/{id}</c> and <c>DELETE /api/v1/highscores/games/{gameId}</c>.
                /// </summary>
                /// <param name="app">The endpoint route builder.</param>
                /// <returns>The route builder.</returns>
                public static IEndpointRouteBuilder MapDeleteHighscores(this IEndpointRouteBuilder app)
                {
                        // Restrict Hub-side soft deletes to authorized Hub administrators.
                        app.MapDelete("/api/v1/highscores/{id:guid}", HandleDeleteByIdAsync)
                                .RequireAuthorization(HubAuthSetup.POLICY_HUB_ADMIN)
                                .WithName("HubDeleteHighscore");

                        app.MapDelete("/api/v1/highscores/games/{gameId:guid}", HandleDeleteByGameAsync)
                                .RequireAuthorization(HubAuthSetup.POLICY_HUB_ADMIN)
                                .WithName("HubDeleteGameHighscores");

                        return app;
                }

                /// <summary>
                /// Soft-deletes a single highscore entry by its unique identifier.
                /// </summary>
                /// <param name="id">Highscore entry identifier.</param>
                /// <param name="handler">Delete highscore command handler.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns><c>204 No Content</c> when deleted.</returns>
                private static async Task<IResult> HandleDeleteByIdAsync(
                        [FromRoute] Guid id,
                        [FromServices] IDeleteHighscoreCommandHandler handler,
                        CancellationToken cancellationToken)
                {
                        await handler.HandleAsync(new DeleteHighscoreCommand(id), cancellationToken);
                        return Results.NoContent();
                }

                /// <summary>
                /// Soft-deletes all highscore entries for a deleted game and records a sync tombstone.
                /// </summary>
                /// <param name="gameId">Game identifier.</param>
                /// <param name="handler">Delete highscore command handler.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>The count of affected highscore entries.</returns>
                private static async Task<IResult> HandleDeleteByGameAsync(
                        [FromRoute] Guid gameId,
                        [FromServices] IDeleteGameHighscoresCommandHandler handler,
                        CancellationToken cancellationToken)
                {
                        int deletedCount = await handler.HandleAsync(new DeleteGameHighscoresCommand(gameId), cancellationToken);
                        return Results.Ok(new { DeletedCount = deletedCount });
                }
        }
}

