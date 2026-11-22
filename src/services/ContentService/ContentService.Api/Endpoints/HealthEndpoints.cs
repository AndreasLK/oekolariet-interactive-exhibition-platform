using ContentService.Api.Common;
using ContentService.Application.Models;
using ContentService.Application.UseCases.Health;

namespace ContentService.Api.Endpoints;

/// <summary>
/// Exposes storage quota telemetry over HTTP so exhibition admin dashboards and monitors can detect capacity pressure before image uploads fail with HTTP 507.
/// </summary>
internal static class HealthEndpoints
{
        /// <summary>
        /// Wires the storage health route into the API pipeline with OpenAPI metadata for client tooling.
        /// </summary>
        /// <param name="endpoints">The ASP.NET Core endpoint route builder.</param>
        /// <returns>The updated endpoint route builder for fluent startup registration.</returns>
        public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
        {
                ArgumentNullException.ThrowIfNull(endpoints);

                endpoints.MapGet(pattern: ApiConstants.STORAGE_HEALTH_ROUTE, handler: HandleGetStorageHealthAsync)
                        .WithTags(ApiConstants.HEALTH_OPENAPI_TAG)
                        .Produces<StorageHealthResult>(StatusCodes.Status200OK);

                return endpoints;
        }

        /// <summary>
        /// Delegates quota calculation to <see cref="GetStorageHealthUseCase"/> so HTTP routing stays decoupled from threshold rules.
        /// </summary>
        /// <param name="useCase">The storage health application use case.</param>
        /// <param name="cancellationToken">Propagates HTTP request aborts to cancel pending SQL aggregation.</param>
        /// <returns>An HTTP 200 OK response containing the <see cref="StorageHealthResult"/>.</returns>
        private static async Task<IResult> HandleGetStorageHealthAsync(
                GetStorageHealthUseCase useCase,
                CancellationToken cancellationToken)
        {
                StorageHealthResult health = await useCase.ExecuteAsync(cancellationToken: cancellationToken);
                return Results.Ok(health);
        }
}
