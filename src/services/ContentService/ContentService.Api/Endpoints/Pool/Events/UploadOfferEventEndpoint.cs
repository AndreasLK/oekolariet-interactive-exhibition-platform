using System.Text.Json;
using ContentService.Facade.Dtos;
using ContentService.Facade.Interfaces;

namespace ContentService.Api.Endpoints.Pool;

/// <summary>
/// Handles Dapr Pub/Sub <c>upload-offer</c> broadcast events.
/// </summary>
internal static class UploadOfferEventEndpoint
{
        /// <summary>
        /// Replies only if this node is healthy and has enough remaining quota for the requested upload.
        /// </summary>
        /// <param name="rawEvent">The incoming CloudEvent or raw JSON payload.</param>
        /// <param name="platformQueryFacade">The platform health query facade.</param>
        /// <param name="poolFacade">The pool facade used to send the reply.</param>
        /// <param name="cancellationToken">Propagates cancellation.</param>
        /// <returns>Always returns <c>Results.Ok()</c> so Dapr marks the message handled.</returns>
        public static async Task<IResult> HandleAsync(
                JsonElement rawEvent,
                IContentPlatformQueryFacade platformQueryFacade,
                IPoolFacade poolFacade,
                CancellationToken cancellationToken)
        {
                JsonElement payload = CloudEventPayloadReader.UnwrapCloudEventData(rawEvent: rawEvent);
                if (!CloudEventPayloadReader.TryReadGuid(element: payload, propertyName: "correlationId", value: out Guid correlationId))
                {
                        return Results.Ok();
                }

                string? replyToAppId = CloudEventPayloadReader.ReadString(element: payload, propertyName: "replyToAppId");
                string? replyTopic = CloudEventPayloadReader.ReadString(element: payload, propertyName: "replyTopic");
                if (string.IsNullOrWhiteSpace(replyToAppId) && string.IsNullOrWhiteSpace(replyTopic))
                {
                        return Results.Ok();
                }

                long requiredBytes = CloudEventPayloadReader.ReadInt64(element: payload, propertyName: "requiredBytes");
                StorageHealthDto health = await platformQueryFacade.GetStorageHealthAsync(cancellationToken: cancellationToken);
                long remainingBytes = Math.Max(val1: 0L, val2: health.QuotaBytes - health.UsedBytes);

                // WHY: Only suggest this node when storage health is not FullReadOnly and remaining quota covers the upload.
                bool isHealthyForUpload = health.Status != StorageHealthStatusDto.FullReadOnly
                        && remainingBytes >= requiredBytes;
                if (!isHealthyForUpload)
                {
                        return Results.Ok();
                }

                await poolFacade.SendLocalOfferReplyAsync(
                        replyToAppId: replyToAppId,
                        replyTopic: replyTopic,
                        correlationId: correlationId,
                        availableBytes: remainingBytes,
                        cancellationToken: cancellationToken);

                return Results.Ok();
        }
}
