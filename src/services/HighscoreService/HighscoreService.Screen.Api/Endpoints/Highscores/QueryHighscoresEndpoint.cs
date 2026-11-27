using HighscoreService.Application.Interfaces.Queries;
using HighscoreService.Facade.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace HighscoreService.Screen.Api.Endpoints.Highscores
{
        /// <summary>
        /// Maps GET and POST specification query endpoints on a Screen server.
        /// </summary>
        public static class QueryHighscoresEndpoint
        {
                /// <summary>
                /// Registers <c>GET /api/v1/highscores</c> and <c>POST /api/v1/highscores/query</c>.
                /// </summary>
                /// <param name="app">The endpoint route builder.</param>
                /// <returns>The route builder.</returns>
                public static IEndpointRouteBuilder MapQueryHighscores(this IEndpointRouteBuilder app)
                {
                        // Expose both GET and POST query routes to mirror the Hub API contract during failover.
                        app.MapGet("/api/v1/highscores", HandleGetAsync)
                                .WithName("ScreenGetHighscores");

                        app.MapPost("/api/v1/highscores/query", HandlePostAsync)
                                .WithName("ScreenQueryHighscores");

                        return app;
                }

                /// <summary>
                /// Executes a query-string leaderboard query against the local Screen store.
                /// </summary>
                private static async Task<IResult> HandleGetAsync(
                        [AsParameters] HighscoreQueryDto query,
                        [FromServices] IQueryHighscoresHandler handler,
                        CancellationToken cancellationToken)
                {
                        // Query locally cached highscores within the Screen's 7-day retention window.
                        PagedDto<HighscoreDto> result = await handler.HandleAsync(query, cancellationToken);
                        return Results.Ok(result);
                }

                /// <summary>
                /// Executes a JSON-body leaderboard query against the local Screen store.
                /// </summary>
                private static async Task<IResult> HandlePostAsync(
                        [FromBody] HighscoreQueryDto query,
                        [FromServices] IQueryHighscoresHandler handler,
                        CancellationToken cancellationToken)
                {
                        // Delegate to the shared query handler for consistent filtering and paging.
                        PagedDto<HighscoreDto> result = await handler.HandleAsync(query, cancellationToken);
                        return Results.Ok(result);
                }
        }
}

