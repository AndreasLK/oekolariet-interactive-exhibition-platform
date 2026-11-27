using HighscoreService.Application.Interfaces.CommandHandlers;
using HighscoreService.Facade.Commands;
using Microsoft.AspNetCore.Mvc;

namespace HighscoreService.Screen.Api.Endpoints.Highscores
{
        /// <summary>
        /// Maps soft-delete endpoints on a Screen server.
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
                        // Screen API remains open on the local kiosk network without Hub JWT requirements.
                        app.MapDelete("/api/v1/highscores/{id:guid}", HandleDeleteByIdAsync)
                                .WithName("ScreenDeleteHighscore");

                        app.MapDelete("/api/v1/highscores/games/{gameId:guid}", HandleDeleteByGameAsync)
                                .WithName("ScreenDeleteGameHighscores");

                        return app;
                }

                /// <summary>
                /// Soft-deletes a single local highscore entry.
                /// </summary>
                private static async Task<IResult> HandleDeleteByIdAsync(
                        [FromRoute] Guid id,
                        [FromServices] IDeleteHighscoreCommandHandler handler,
                        CancellationToken cancellationToken)
                {
                        // Queue soft deletion in the local store and outbox.
                        await handler.HandleAsync(new DeleteHighscoreCommand(id), cancellationToken);
                        return Results.NoContent();
                }

                /// <summary>
                /// Soft-deletes all local highscores for a game and records a local tombstone.
                /// </summary>
                private static async Task<IResult> HandleDeleteByGameAsync(
                        [FromRoute] Guid gameId,
                        [FromServices] IDeleteGameHighscoresCommandHandler handler,
                        CancellationToken cancellationToken)
                {
                        // Soft-delete matching entries locally and report the affected count.
                        int deletedCount = await handler.HandleAsync(new DeleteGameHighscoresCommand(gameId), cancellationToken);
                        return Results.Ok(new { DeletedCount = deletedCount });
                }
        }
}

