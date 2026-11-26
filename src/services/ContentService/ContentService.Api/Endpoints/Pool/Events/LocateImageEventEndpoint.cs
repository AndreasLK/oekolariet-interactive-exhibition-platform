using System.Text.Json;
using ContentService.Facade.Dtos;
using ContentService.Facade.Interfaces;

namespace ContentService.Api.Endpoints.Pool;

/// <summary>
/// Handles Dapr Pub/Sub <c>locate-image</c> broadcast events.
/// </summary>
internal static class LocateImageEventEndpoint
{
        /// <summary>
        /// Replies only if this node owns <c>imageId</c> in its local database.
        /// </summary>
        /// <remarks>
        /// Non-standard HTTP semantics: Unlike REST endpoints that return <c>400</c> or <c>404</c>, this handler always
        /// returns <c>200 OK</c> on malformed events or local misses.
        /// <para>
        /// What we gain: Dapr Pub/Sub treats non-2xx responses as transient faults and retries/dead-letters them; returning
        /// <c>200 OK</c> prevents cluster-wide retry storms during scatter-gather broadcasts.
        /// What we lose: Callers must rely on their scatter-gather timeout when no node owns the image.
        /// </para>
        /// </remarks>
        /// <param name="rawEvent">The incoming CloudEvent or raw JSON payload.</param>
        /// <param name="imageQueryFacade">The image query facade.</param>
        /// <param name="platformQueryFacade">The platform health query facade.</param>
        /// <param name="poolFacade">The pool facade used to send the reply.</param>
        /// <param name="cancellationToken">Propagates cancellation.</param>
        /// <returns>Always returns <c>Results.Ok()</c> so Dapr marks the message handled.</returns>
        public static async Task<IResult> HandleAsync(
                JsonElement rawEvent,
                IImageQueryFacade imageQueryFacade,
                IContentPlatformQueryFacade platformQueryFacade,
                IPoolFacade poolFacade,
                CancellationToken cancellationToken)
        {
                JsonElement payload = CloudEventPayloadReader.UnwrapCloudEventData(rawEvent: rawEvent);
                if (!CloudEventPayloadReader.TryReadGuid(element: payload, propertyName: PoolEventConstants.CORRELATION_ID_PROPERTY, value: out Guid correlationId)
                        || !CloudEventPayloadReader.TryReadGuid(element: payload, propertyName: PoolEventConstants.IMAGE_ID_PROPERTY, value: out Guid imageId))
                {
                        return Results.Ok();
                }

                string? replyToAppId = CloudEventPayloadReader.ReadString(element: payload, propertyName: PoolEventConstants.REPLY_TO_APP_ID_PROPERTY);
                string? replyTopic = CloudEventPayloadReader.ReadString(element: payload, propertyName: PoolEventConstants.REPLY_TOPIC_PROPERTY);
                if (string.IsNullOrWhiteSpace(replyToAppId) && string.IsNullOrWhiteSpace(replyTopic))
                {
                        return Results.Ok();
                }

                try
                {
                        // WHY: Check strictly local DB (allowPeerFallback: false) so a broadcast never triggers cascading peer broadcasts.
                        await imageQueryFacade.GetImageMetadataAsync(
                                imageId: imageId,
                                cancellationToken: cancellationToken,
                                storageNodeId: null,
                                allowPeerFallback: false);
                }
                catch (KeyNotFoundException)
                {
                        // Not on this node: stay silent as required by scatter-gather.
                        return Results.Ok();
                }

                StorageHealthDto health = await platformQueryFacade.GetStorageHealthAsync(cancellationToken: cancellationToken);
                long remainingBytes = Math.Max(val1: 0L, val2: health.QuotaBytes - health.UsedBytes);
                await poolFacade.SendLocalOfferReplyAsync(
                        replyToAppId: replyToAppId,
                        replyTopic: replyTopic,
                        correlationId: correlationId,
                        availableBytes: remainingBytes,
                        cancellationToken: cancellationToken);

                return Results.Ok();
        }
}
