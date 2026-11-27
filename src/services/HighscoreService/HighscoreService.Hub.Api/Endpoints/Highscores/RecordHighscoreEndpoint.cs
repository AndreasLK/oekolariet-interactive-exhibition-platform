using HighscoreService.Application.Interfaces.CommandHandlers;
using HighscoreService.Facade.Commands;
using HighscoreService.Facade.Dtos;
using HighscoreService.Hub.Api.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace HighscoreService.Hub.Api.Endpoints.Highscores
{
        /// <summary>
        /// Maps the endpoint for recording new highscore and abandoned-round entries on the Hub.
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
                        // Require either an approved Screen server (scr_ token) or a logged-in AuthService user (JWT)
                        // so anonymous internet traffic cannot spam fake scores into the central Hub database.
                        app.MapPost("/api/v1/highscores", HandleAsync)
                                .RequireAuthorization(HubAuthSetup.POLICY_SCREEN_OR_USER)
                                .WithName("HubRecordHighscore");

                        return app;
                }

                /// <summary>
                /// Handles <c>POST /api/v1/highscores</c> and returns a proxy-aware <c>201 Created</c> response.
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

                        // Prepend Request.PathBase (populated by UsePathBase or X-Forwarded-Prefix) so
                        // external proxy callers receive /<prefix>/api/v1/highscores/{id} while direct LAN
                        // callers still receive /api/v1/highscores/{id}.
                        return Results.Created($"{httpContext.Request.PathBase}/api/v1/highscores/{result.Id}", result);
                }
        }
}

