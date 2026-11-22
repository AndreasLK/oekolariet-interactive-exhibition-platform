using ContentService.Api.Common;
using ContentService.Application.Models;
using ContentService.Application.UseCases.Publishing;

namespace ContentService.Api.Endpoints;

/// <summary>
/// Exposes offline kiosk bundle resolution over HTTP so exhibition publishers can validate asset links and obtain a sealed bundle checksum before deployment.
/// </summary>
internal static class PublishingEndpoints
{
        /// <summary>
        /// Registers the bundle resolution endpoint as a <c>POST</c> action because unsaved implementation JSON payloads can be large and must not sit in query strings.
        /// </summary>
        /// <param name="endpoints">The ASP.NET Core endpoint route builder.</param>
        /// <returns>The updated endpoint route builder for fluent startup registration.</returns>
        public static IEndpointRouteBuilder MapPublishingEndpoints(this IEndpointRouteBuilder endpoints)
        {
                ArgumentNullException.ThrowIfNull(endpoints);

                endpoints.MapPost(pattern: ApiConstants.PUBLISH_BUNDLES_ROUTE, handler: HandleResolvePublishBundleAsync)
                        .WithTags(ApiConstants.PUBLISHING_OPENAPI_TAG)
                        .Produces<PublishBundleResult>(StatusCodes.Status200OK)
                        .ProducesProblem(StatusCodes.Status400BadRequest)
                        .ProducesProblem(StatusCodes.Status404NotFound);

                return endpoints;
        }

        /// <summary>
        /// Delegates bundle verification and deterministic checksum generation to <see cref="ResolvePublishBundleUseCase"/> so HTTP handlers carry no hashing or reference rules.
        /// </summary>
        /// <param name="command">The publish bundle resolution request.</param>
        /// <param name="useCase">The resolve publish bundle application use case.</param>
        /// <param name="cancellationToken">Propagates client disconnects to abort database lookups.</param>
        /// <returns>An HTTP 200 OK response containing the resolved <see cref="PublishBundleResult"/>.</returns>
        private static async Task<IResult> HandleResolvePublishBundleAsync(
                ResolvePublishBundleCommand command,
                ResolvePublishBundleUseCase useCase,
                CancellationToken cancellationToken)
        {
                PublishBundleResult bundle = await useCase.ExecuteAsync(
                        command: command,
                        cancellationToken: cancellationToken);

                return Results.Ok(bundle);
        }
}
