using ContentService.Api.Common;
using ContentService.Api.Mapper;
using ContentService.Facade.Dtos;
using ContentService.Facade.Interfaces;
using ContentService.Sdk.Models;

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

                // Register POST bundle resolution so large implementation JSON payloads are sent in the request body.
                endpoints.MapPost(pattern: ApiConstants.PUBLISH_BUNDLES_RELATIVE_ROUTE, handler: HandleResolvePublishBundleAsync)
                        .WithTags(ApiConstants.PUBLISHING_OPENAPI_TAG)
                        .Produces<ContentPublishBundleResponse>(StatusCodes.Status200OK)
                        .ProducesProblem(StatusCodes.Status400BadRequest)
                        .ProducesProblem(StatusCodes.Status404NotFound);

                return endpoints;
        }

        /// <summary>
        /// Delegates bundle verification and deterministic checksum generation to <see cref="IContentPlatformQueryFacade"/> so HTTP handlers carry no hashing or reference rules.
        /// </summary>
        /// <param name="query">The publish bundle resolution request.</param>
        /// <param name="queryFacade">The CQS platform query facade.</param>
        /// <param name="cancellationToken">Propagates client disconnects to abort database lookups.</param>
        /// <returns>An HTTP 200 OK response containing the resolved <see cref="ContentPublishBundleResponse"/>.</returns>
        private static async Task<IResult> HandleResolvePublishBundleAsync(
                ContentResolvePublishBundleQuery query,
                IContentPlatformQueryFacade queryFacade,
                CancellationToken cancellationToken)
        {
                // Resolve all referenced assets, compute the bundle SHA-256 checksum, and map to the SDK response.
                PublishBundleDto bundle = await queryFacade.ResolvePublishBundleAsync(
                        query: PlatformMapper.ToQuery(query: query),
                        cancellationToken: cancellationToken);

                return Results.Ok(value: PlatformMapper.ToResponse(dto: bundle));
        }
}
