using HighscoreService.Application.Interfaces.Queries;
using HighscoreService.Facade.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace HighscoreService.Screen.Api.Endpoints.Sync
{
        /// <summary>
        /// Maps the read-only local delta endpoint so LAN peers can pull missed scores directly.
        /// </summary>
        public static class ScreenDeltaEndpoint
        {
                private const int DEFAULT_BATCH_SIZE = 100;

                /// <summary>
                /// Registers <c>GET /api/v1/highscores/sync/delta</c>.
                /// </summary>
                /// <param name="app">The endpoint route builder.</param>
                /// <returns>The route builder.</returns>
                public static IEndpointRouteBuilder MapScreenDeltaEndpoint(this IEndpointRouteBuilder app)
                {
                        // Expose local delta records so peer screens can sync over LAN when the Hub is offline.
                        app.MapGet("/api/v1/highscores/sync/delta", HandleGetDeltaAsync)
                                .WithName("ScreenGetSyncDelta");

                        return app;
                }

                /// <summary>
                /// Serves incremental highscore deltas from this Screen's local SQLite store.
                /// </summary>
                private static async Task<IResult> HandleGetDeltaAsync(
                        [FromQuery] Guid? gameId,
                        [FromQuery] long afterSequence,
                        [FromQuery] DateTimeOffset? sinceUtc,
                        [FromQuery] int? batchSize,
                        [FromServices] IScreenDeltaQueries queries,
                        CancellationToken cancellationToken)
                {
                        // Apply default batch size when the peer omits the query parameter.
                        int effectiveSize = batchSize.GetValueOrDefault(DEFAULT_BATCH_SIZE);
                        IReadOnlyList<HighscoreDto> delta = await queries.GetLocalDeltaAsync(
                                gameId: gameId,
                                afterSequence: afterSequence,
                                sinceUtc: sinceUtc,
                                batchSize: effectiveSize,
                                cancellationToken: cancellationToken);

                        return Results.Ok(delta);
                }
        }
}

