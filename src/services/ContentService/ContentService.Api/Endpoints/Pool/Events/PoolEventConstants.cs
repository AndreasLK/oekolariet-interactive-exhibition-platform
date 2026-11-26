namespace ContentService.Api.Endpoints.Pool;

/// <summary>
/// Centralizes CloudEvent JSON payload property names shared by Dapr Pub/Sub pool event handlers.
/// </summary>
internal static class PoolEventConstants
{
        /// <summary>
        /// JSON property identifying the scatter-gather correlation GUID.
        /// </summary>
        public const string CORRELATION_ID_PROPERTY = "correlationId";

        /// <summary>
        /// JSON property identifying the target image GUID in locate-image broadcasts.
        /// </summary>
        public const string IMAGE_ID_PROPERTY = "imageId";

        /// <summary>
        /// JSON property identifying the requesting Dapr app ID for direct reply invocation.
        /// </summary>
        public const string REPLY_TO_APP_ID_PROPERTY = "replyToAppId";

        /// <summary>
        /// JSON property identifying the reply route or topic on the requesting node.
        /// </summary>
        public const string REPLY_TOPIC_PROPERTY = "replyTopic";

        /// <summary>
        /// JSON property specifying the requested upload size in bytes for upload-offer broadcasts.
        /// </summary>
        public const string REQUIRED_BYTES_PROPERTY = "requiredBytes";
}

