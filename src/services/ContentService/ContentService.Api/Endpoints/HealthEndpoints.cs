using ContentService.Api.Common;
using ContentService.Facade.Dtos;
using ContentService.Facade.Interfaces;

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

                // Register the storage quota health endpoint under the versioned API group.
                endpoints.MapGet(pattern: ApiConstants.STORAGE_HEALTH_RELATIVE_ROUTE, handler: HandleGetStorageHealthAsync)
                        .WithTags(ApiConstants.HEALTH_OPENAPI_TAG)
                        .Produces<StorageHealthDto>(StatusCodes.Status200OK);

                return endpoints;
        }

        /// <summary>
        /// Delegates quota calculation to <see cref="IContentPlatformQueryFacade"/> so HTTP routing stays decoupled from threshold rules.
        /// </summary>
        /// <param name="queryFacade">The CQS platform query facade.</param>
        /// <param name="cancellationToken">Propagates HTTP request aborts to cancel pending SQL aggregation.</param>
        /// <returns>An HTTP 200 OK response containing the <see cref="StorageHealthDto"/>.</returns>
        private static async Task<IResult> HandleGetStorageHealthAsync(
                IContentPlatformQueryFacade queryFacade,
                CancellationToken cancellationToken)
        {
                // Query current storage utilization and return 200 OK.
                StorageHealthDto health = await queryFacade.GetStorageHealthAsync(cancellationToken: cancellationToken);
                return Results.Ok(health);
        }
}
