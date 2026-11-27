using HighscoreService.Application.Interfaces.CommandHandlers;
using HighscoreService.Facade.Commands;
using HighscoreService.Facade.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace HighscoreService.Screen.Api.Endpoints.Highscores
{
        /// <summary>
        /// Maps the endpoint for recording new highscore and abandoned-round entries on a Screen server.
        /// </summary>
        public static class RecordHighscoreEndpoint
        {
                /// <summary>
                /// Registers <c>POST /api/v1/highscores</c>.
                /// </summary>
                /// <param name="app">The endpoint route builder.</param>
                /// <returns>The route builder.</returns>
                public static IEndpointRouteBuilder MapRecordHighscore(this IEndpointRouteBuilder app)
                {
                        // Screen.Api runs inside the isolated exhibition LAN and intentionally has no auth middleware
                        // so local game kiosks can record scores even when the central Hub and AuthService are offline.
                        app.MapPost("/api/v1/highscores", HandleAsync)
                                .WithName("ScreenRecordHighscore");

                        return app;
                }

                /// <summary>
                /// Handles <c>POST /api/v1/highscores</c> and returns a path-base-aware <c>201 Created</c> response.
                /// </summary>
                /// <param name="request">Highscore submission payload.</param>
                /// <param name="handler">Record highscore command handler.</param>
                /// <param name="httpContext">Current HTTP context used to read <see cref="HttpRequest.PathBase"/>.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>The created highscore response.</returns>
                private static async Task<IResult> HandleAsync(
                        [FromBody] RecordHighscoreCommand request,
                        [FromServices] IRecordHighscoreCommandHandler handler,
                        HttpContext httpContext,
                        CancellationToken cancellationToken)
                {
                        HighscoreDto result = await handler.HandleAsync(request, cancellationToken);

                        // Prepend Request.PathBase to stay consistent with Hub.Api if a Screen API is ever mounted under a prefix.
                        return Results.Created($"{httpContext.Request.PathBase}/api/v1/highscores/{result.Id}", result);
                }
        }
}

