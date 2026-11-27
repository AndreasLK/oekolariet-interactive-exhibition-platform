using HighscoreService.Application.Interfaces.Queries;
using HighscoreService.Facade.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace HighscoreService.Hub.Api.Endpoints.Highscores
{
        /// <summary>
        /// Maps GET and POST specification query endpoints on the Hub.
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
                        // Expose both GET (query string) and POST (JSON body) for public leaderboard queries.
                        app.MapGet("/api/v1/highscores", HandleGetAsync)
                                .WithName("HubGetHighscores");

                        app.MapPost("/api/v1/highscores/query", HandlePostAsync)
                                .WithName("HubQueryHighscores");

                        return app;
                }

                /// <summary>
                /// Handles <c>GET /api/v1/highscores</c> using query-string parameters.
                /// </summary>
                /// <param name="query">Query filter and paging parameters.</param>
                /// <param name="handler">Highscore query handler.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>Paged highscore results.</returns>
                private static async Task<IResult> HandleGetAsync(
                        [AsParameters] HighscoreQueryDto query,
                        [FromServices] IQueryHighscoresHandler handler,
                        CancellationToken cancellationToken)
                {
                        PagedDto<HighscoreDto> result = await handler.HandleAsync(query, cancellationToken);
                        return Results.Ok(result);
                }

                /// <summary>
                /// Handles <c>POST /api/v1/highscores/query</c> for complex filter payloads.
                /// </summary>
                /// <remarks>
                /// Exposed alongside <c>GET</c> so reverse proxies with strict URL-length limits never truncate
                /// large multi-filter queries.
                /// </remarks>
                /// <param name="query">Query filter and paging parameters.</param>
                /// <param name="handler">Highscore query handler.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>Paged highscore results.</returns>
                private static async Task<IResult> HandlePostAsync(
                        [FromBody] HighscoreQueryDto query,
                        [FromServices] IQueryHighscoresHandler handler,
                        CancellationToken cancellationToken)
                {
                        PagedDto<HighscoreDto> result = await handler.HandleAsync(query, cancellationToken);
                        return Results.Ok(result);
                }
        }
}

