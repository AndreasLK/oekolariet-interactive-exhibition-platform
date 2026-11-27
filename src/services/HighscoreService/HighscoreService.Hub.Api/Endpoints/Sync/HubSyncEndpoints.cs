using HighscoreService.Application.Interfaces.CommandHandlers;
using HighscoreService.Application.Interfaces.Queries;
using HighscoreService.Facade.Commands;
using HighscoreService.Facade.Dtos;
using HighscoreService.Hub.Api.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace HighscoreService.Hub.Api.Endpoints.Sync
{
        /// <summary>
        /// Maps Hub synchronization endpoints for receiving screen batches and serving per-game deltas and tombstones.
        /// </summary>
        public static class HubSyncEndpoints
        {
                private const int DEFAULT_BATCH_SIZE = 100;

                /// <summary>
                /// Registers <c>/api/v1/highscores/sync/batch</c>, <c>/sync/delta</c>, and <c>/sync/tombstones</c>.
                /// </summary>
                /// <param name="app">The endpoint route builder.</param>
                /// <returns>The route builder.</returns>
                public static IEndpointRouteBuilder MapHubSyncEndpoints(this IEndpointRouteBuilder app)
                {
                        // Require an approved Screen token (Bearer scr_...) or Hub Admin JWT.
                        // Unapproved/pending screens receive HTTP 401 here, which signals HighscoreScreenSyncWorker
                        // on first boot to auto-enroll via POST /api/v1/screens/enroll and await admin approval.
                        RouteGroupBuilder syncGroup = app.MapGroup("/api/v1/highscores/sync")
                                .RequireAuthorization(HubAuthSetup.POLICY_SCREEN_ONLY);

                        syncGroup.MapPost("/batch", HandleSyncBatchAsync)
                                .WithName("HubApplySyncBatch");

                        syncGroup.MapGet("/delta", HandleGetDeltaAsync)
                                .WithName("HubGetSyncDelta");

                        syncGroup.MapGet("/tombstones", HandleGetTombstonesAsync)
                                .WithName("HubGetSyncTombstones");

                        return app;
                }

                /// <summary>
                /// Ingests a batch of highscore entries pushed from an exhibition Screen's outbox.
                /// </summary>
                /// <param name="entries">Highscore entries to upsert on the Hub.</param>
                /// <param name="handler">Batch sync command handler.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>Batch ingestion counts.</returns>
                private static async Task<IResult> HandleSyncBatchAsync(
                        [FromBody] IReadOnlyList<HighscoreDto> entries,
                        [FromServices] IApplySyncBatchCommandHandler handler,
                        CancellationToken cancellationToken)
                {
                        // Ingestion is idempotent by entry Id so retried batches over flaky LAN/WAN links never duplicate scores.
                        SyncBatchResultDto result = await handler.HandleAsync(new ApplySyncBatchCommand(entries), cancellationToken);
                        return Results.Ok(result);
                }

                /// <summary>
                /// Returns incremental highscore changes after a sync watermark, optionally scoped to one game.
                /// </summary>
                /// <param name="gameId">Optional game filter.</param>
                /// <param name="afterSequence">Exclusive Hub sync sequence watermark.</param>
                /// <param name="sinceUtc">Optional lower timestamp bound for initial bootstrap.</param>
                /// <param name="batchSize">Maximum number of entries to return.</param>
                /// <param name="queries">Hub sync queries.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>Ordered delta entries.</returns>
                private static async Task<IResult> HandleGetDeltaAsync(
                        [FromQuery] Guid? gameId,
                        [FromQuery] long afterSequence,
                        [FromQuery] DateTimeOffset? sinceUtc,
                        [FromQuery] int? batchSize,
                        [FromServices] IHubSyncQueries queries,
                        CancellationToken cancellationToken)
                {
                        int effectiveSize = batchSize.GetValueOrDefault(DEFAULT_BATCH_SIZE);
                        IReadOnlyList<HighscoreDto> delta = await queries.GetDeltaAsync(
                                gameId: gameId,
                                afterSequence: afterSequence,
                                sinceUtc: sinceUtc,
                                batchSize: effectiveSize,
                                cancellationToken: cancellationToken);

                        return Results.Ok(delta);
                }

                /// <summary>
                /// Returns game-level deletion tombstones recorded after a sync watermark.
                /// </summary>
                /// <param name="afterSequence">Exclusive tombstone sync sequence watermark.</param>
                /// <param name="queries">Hub sync queries.</param>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>Ordered game deletion tombstones.</returns>
                private static async Task<IResult> HandleGetTombstonesAsync(
                        [FromQuery] long afterSequence,
                        [FromServices] IHubSyncQueries queries,
                        CancellationToken cancellationToken)
                {
                        IReadOnlyList<GameDeletionTombstoneDto> tombstones = await queries.GetGameDeletionTombstonesAsync(
                                afterSequence: afterSequence,
                                cancellationToken: cancellationToken);

                        return Results.Ok(tombstones);
                }
        }
}

