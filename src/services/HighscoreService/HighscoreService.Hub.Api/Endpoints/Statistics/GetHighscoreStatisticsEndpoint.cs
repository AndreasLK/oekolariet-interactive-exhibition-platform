using HighscoreService.Application.Interfaces.Queries;
using HighscoreService.Application.Models;
using HighscoreService.Facade.Dtos;
using HighscoreService.Hub.Api.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace HighscoreService.Hub.Api.Endpoints.Statistics
{
        /// <summary>
        /// Maps Hub endpoints for generating detailed exhibition statistics reports.
        /// </summary>
        public static class GetHighscoreStatisticsEndpoint
        {
                /// <summary>
                /// Registers <c>GET /api/v1/highscores/statistics</c> and <c>POST /api/v1/highscores/statistics</c>.
                /// </summary>
                /// <param name="app">The endpoint route builder.</param>
                /// <returns>The route builder.</returns>
                public static IEndpointRouteBuilder MapHighscoreStatistics(this IEndpointRouteBuilder app)
                {
                        // Require an authenticated AuthService user for exhibition analytics queries.
                        app.MapGet("/api/v1/highscores/statistics", HandleGetAsync)
                                .RequireAuthorization(HubAuthSetup.POLICY_AUTHENTICATED_USER)
                                .WithName("HubGetHighscoreStatistics");

                        app.MapPost("/api/v1/highscores/statistics", HandlePostAsync)
                                .RequireAuthorization(HubAuthSetup.POLICY_AUTHENTICATED_USER)
                                .WithName("HubQueryHighscoreStatistics");

                        return app;
                }

                /// <summary>
                /// Handles <c>GET /api/v1/highscores/statistics</c> using query-string parameters.
                /// </summary>
                /// <param name="query">Statistics filter parameters.</param>
                /// <param name="handler">Hub statistics query handler.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>Aggregated exhibition statistics report.</returns>
                private static async Task<IResult> HandleGetAsync(
                        [AsParameters] HighscoreQueryDto query,
                        [FromServices] IHighscoreStatisticsQueryHandler handler,
                        CancellationToken cancellationToken)
                {
                        HighscoreStatisticsReport report = await handler.HandleAsync(query, cancellationToken);
                        return Results.Ok(report);
                }

                /// <summary>
                /// Handles <c>POST /api/v1/highscores/statistics</c> for complex filter payloads.
                /// </summary>
                /// <param name="query">Statistics filter parameters.</param>
                /// <param name="handler">Hub statistics query handler.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>Aggregated exhibition statistics report.</returns>
                private static async Task<IResult> HandlePostAsync(
                        [FromBody] HighscoreQueryDto query,
                        [FromServices] IHighscoreStatisticsQueryHandler handler,
                        CancellationToken cancellationToken)
                {
                        HighscoreStatisticsReport report = await handler.HandleAsync(query, cancellationToken);
                        return Results.Ok(report);
                }
        }
}

