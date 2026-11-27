using ContentService.Api.Contracts;
using ContentService.Api.Endpoints.Pool;
using ContentService.Facade.Dtos;

namespace ContentService.Api.Endpoints;

/// <summary>
/// Exposes Dapr Pub/Sub subscription discovery and wires stateless pool scatter-gather endpoints.
/// </summary>
internal static class PoolEndpoints
{
        private const string DAPR_SUBSCRIBE_ROUTE = "/dapr/subscribe";
        private const string LOCATE_IMAGE_EVENT_ROUTE = "/pool/events/locate-image";
        private const string UPLOAD_OFFER_EVENT_ROUTE = "/pool/events/upload-offer";
        private const string POOL_REPLIES_ROUTE = "/pool/replies/{correlationId:guid}";
        private const string REQUEST_UPLOAD_OFFER_ROUTE = "/pool/upload-offers";
        private const string LOCATE_IMAGE_TOPIC = "content.pool.locate-image";
        private const string UPLOAD_OFFER_TOPIC = "content.pool.upload-offer";
        private const string POOL_OPENAPI_TAG = "Pool";

        /// <summary>
        /// Maps the version-neutral <c>/dapr/subscribe</c> discovery endpoint so Dapr sidecars automatically subscribe each node to pool topics.
        /// </summary>
        /// <param name="endpoints">The ASP.NET Core endpoint route builder.</param>
        /// <param name="configuration">Application configuration providing the Pub/Sub component name.</param>
        /// <returns>The updated endpoint route builder.</returns>
        public static IEndpointRouteBuilder MapDaprSubscriptions(
                this IEndpointRouteBuilder endpoints,
                IConfiguration configuration)
        {
                ArgumentNullException.ThrowIfNull(endpoints);
                ArgumentNullException.ThrowIfNull(configuration);

                // Resolve the configured Dapr Pub/Sub component name, falling back to the default component.
                string pubSubName = configuration.GetValue<string>("ContentStorage:PubSubName") ?? "contentservice-pubsub";

                // Return the static subscription list Dapr sidecars poll at startup; excluded from OpenAPI.
                endpoints.MapGet(pattern: DAPR_SUBSCRIBE_ROUTE, handler: () => Results.Ok(new[]
                {
                        new DaprSubscriptionItem(
                                Pubsubname: pubSubName,
                                Topic: LOCATE_IMAGE_TOPIC,
                                Route: $"/api/v1{LOCATE_IMAGE_EVENT_ROUTE}"),
                        new DaprSubscriptionItem(
                                Pubsubname: pubSubName,
                                Topic: UPLOAD_OFFER_TOPIC,
                                Route: $"/api/v1{UPLOAD_OFFER_EVENT_ROUTE}")
                }))
                .ExcludeFromDescription();

                return endpoints;
        }

        /// <summary>
        /// Maps the versioned <c>/api/v1/pool/...</c> endpoints for scatter-gather event handling, replies, and healthy upload offer requests.
        /// </summary>
        /// <param name="endpoints">The ASP.NET Core endpoint route builder.</param>
        /// <returns>The updated endpoint route builder.</returns>
        public static IEndpointRouteBuilder MapPoolEndpoints(this IEndpointRouteBuilder endpoints)
        {
                ArgumentNullException.ThrowIfNull(endpoints);

                // Map Dapr Pub/Sub event handlers and internal reply callbacks (hidden from public OpenAPI).
                endpoints.MapPost(pattern: LOCATE_IMAGE_EVENT_ROUTE, handler: LocateImageEventEndpoint.HandleAsync)
                        .WithTags(POOL_OPENAPI_TAG)
                        .ExcludeFromDescription();

                endpoints.MapPost(pattern: UPLOAD_OFFER_EVENT_ROUTE, handler: UploadOfferEventEndpoint.HandleAsync)
                        .WithTags(POOL_OPENAPI_TAG)
                        .ExcludeFromDescription();

                endpoints.MapPost(pattern: POOL_REPLIES_ROUTE, handler: PoolReplyEndpoint.Handle)
                        .WithTags(POOL_OPENAPI_TAG)
                        .ExcludeFromDescription();

                // Map the public/SDK-facing upload-offer endpoint for selecting a healthy storage node.
                endpoints.MapPost(pattern: REQUEST_UPLOAD_OFFER_ROUTE, handler: RequestUploadOfferEndpoint.HandleAsync)
                        .WithTags(POOL_OPENAPI_TAG)
                        .Produces<PoolNodeOfferDto>(StatusCodes.Status200OK)
                        .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

                return endpoints;
        }
}

