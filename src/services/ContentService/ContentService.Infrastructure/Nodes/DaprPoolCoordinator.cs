using System.Net.Http.Json;
using ContentService.Application.Interfaces;
using ContentService.Application.Models;
using ContentService.Application.Options;
using Microsoft.Extensions.Options;

namespace ContentService.Infrastructure.Nodes;

/// <summary>
/// Implements stateless Dapr Pub/Sub scatter-gather coordination across an arbitrary pool of <c>ContentService</c> nodes with a 5-second default timeout.
/// </summary>
internal sealed class DaprPoolCoordinator : IPoolCoordinator
{
        /// <summary>
        /// Dapr Pub/Sub topic for broadcasting image-location queries.
        /// </summary>
        public const string LOCATE_IMAGE_TOPIC = "content.pool.locate-image";

        /// <summary>
        /// Dapr Pub/Sub topic for broadcasting healthy upload-capacity requests.
        /// </summary>
        public const string UPLOAD_OFFER_TOPIC = "content.pool.upload-offer";

        private const string DAPR_HTTP_PORT_ENV_VAR = "DAPR_HTTP_PORT";
        private const string DEFAULT_DAPR_HTTP_PORT = "3500";
        private const string DAPR_APP_ID_PREFIX = "contentservice-";
        private const int DEFAULT_TIMEOUT_SECONDS = 5;

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ContentStorageOptions _options;
        private readonly string _daprBaseUrl;
        private readonly string _localAppId;
        private readonly PendingPoolReplyRegistry _replyRegistry = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="DaprPoolCoordinator"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The pooled HTTP client factory.</param>
        /// <param name="options">The configured storage options.</param>
        public DaprPoolCoordinator(
                IHttpClientFactory httpClientFactory,
                IOptions<ContentStorageOptions> options)
        {
                ArgumentNullException.ThrowIfNull(httpClientFactory);
                ArgumentNullException.ThrowIfNull(options);

                this._httpClientFactory = httpClientFactory;
                this._options = options.Value;

                string daprPort = Environment.GetEnvironmentVariable(DAPR_HTTP_PORT_ENV_VAR) ?? DEFAULT_DAPR_HTTP_PORT;
                this._daprBaseUrl = $"http://127.0.0.1:{daprPort.Trim()}";
                this._localAppId = $"{DAPR_APP_ID_PREFIX}{this._options.StorageNodeId.Trim().ToLowerInvariant()}";
        }

        /// <inheritdoc />
        public Task<PoolNodeOffer?> LocateImageOwnerAsync(
                Guid imageId,
                CancellationToken cancellationToken)
        {
                return this.ScatterAndAwaitFirstReplyAsync(
                        topic: LOCATE_IMAGE_TOPIC,
                        buildPayload: correlationId => new
                        {
                                CorrelationId = correlationId,
                                ImageId = imageId,
                                ReplyToAppId = this._localAppId
                        },
                        cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public Task<PoolNodeOffer?> RequestHealthyUploadOfferAsync(
                long requiredBytes,
                CancellationToken cancellationToken)
        {
                return this.ScatterAndAwaitFirstReplyAsync(
                        topic: UPLOAD_OFFER_TOPIC,
                        buildPayload: correlationId => new
                        {
                                CorrelationId = correlationId,
                                RequiredBytes = requiredBytes,
                                ReplyToAppId = this._localAppId
                        },
                        cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public void CompletePendingRequest(
                Guid correlationId,
                PoolNodeOffer offer)
        {
                this._replyRegistry.Complete(correlationId: correlationId, offer: offer);
        }

        /// <inheritdoc />
        public async Task SendReplyAsync(
                string? replyToAppId,
                string? replyTopic,
                Guid correlationId,
                PoolNodeOffer offer,
                CancellationToken cancellationToken)
        {
                ArgumentNullException.ThrowIfNull(offer);

                HttpClient client = this._httpClientFactory.CreateClient(DaprStorageNodeClient.DAPR_HTTP_CLIENT_NAME);

                // Option A: Publish to a caller-specified Pub/Sub reply topic so external callers do not need an HTTP callback endpoint.
                if (!string.IsNullOrWhiteSpace(replyTopic))
                {
                        string topicPublishUrl = $"{this._daprBaseUrl}/v1.0/publish/{this._options.PubSubName}/{replyTopic.Trim()}";
                        using HttpResponseMessage pubResponse = await client.PostAsJsonAsync(
                                requestUri: topicPublishUrl,
                                value: new
                                {
                                        CorrelationId = correlationId,
                                        Offer = offer
                                },
                                options: DaprNodeRequestSender.JSON_OPTIONS,
                                cancellationToken: cancellationToken);
                }

                if (string.IsNullOrWhiteSpace(replyToAppId))
                {
                        return;
                }

                // WHY: Short-circuit in-process when this node is replying to its own scatter request, avoiding an unnecessary Dapr loopback hop.
                if (string.Equals(a: replyToAppId, b: this._localAppId, comparisonType: StringComparison.OrdinalIgnoreCase))
                {
                        this._replyRegistry.Complete(correlationId: correlationId, offer: offer);
                        return;
                }

                // Option B: Direct Dapr service invocation callback to the requesting node's /api/v1/pool/replies/{correlationId} endpoint.
                string replyUrl = $"{this._daprBaseUrl}/v1.0/invoke/{replyToAppId}/method/api/v1/pool/replies/{correlationId:D}";

                using HttpResponseMessage response = await client.PostAsJsonAsync(
                        requestUri: replyUrl,
                        value: offer,
                        options: DaprNodeRequestSender.JSON_OPTIONS,
                        cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Publishes a scatter-gather request to Dapr Pub/Sub and awaits the first peer reply up to the configured timeout.
        /// </summary>
        /// <typeparam name="TPayload">The strongly typed broadcast payload type.</typeparam>
        /// <param name="topic">The Dapr Pub/Sub topic to publish to.</param>
        /// <param name="buildPayload">Factory producing the correlation-tagged request payload.</param>
        /// <param name="cancellationToken">Propagates caller cancellation.</param>
        /// <returns>The first <see cref="PoolNodeOffer"/> received before the timeout, or <see langword="null"/> if none arrived.</returns>
        private async Task<PoolNodeOffer?> ScatterAndAwaitFirstReplyAsync<TPayload>(
                string topic,
                Func<Guid, TPayload> buildPayload,
                CancellationToken cancellationToken)
        {
                Guid correlationId = Guid.NewGuid();
                TaskCompletionSource<PoolNodeOffer> tcs = this._replyRegistry.Register(correlationId: correlationId);

                int timeoutSeconds = this._options.PoolRequestTimeoutSeconds > 0
                        ? this._options.PoolRequestTimeoutSeconds
                        : DEFAULT_TIMEOUT_SECONDS;

                using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(delay: TimeSpan.FromSeconds(timeoutSeconds));

                try
                {
                        HttpClient client = this._httpClientFactory.CreateClient(DaprStorageNodeClient.DAPR_HTTP_CLIENT_NAME);
                        string publishUrl = $"{this._daprBaseUrl}/v1.0/publish/{this._options.PubSubName}/{topic}";

                        using HttpResponseMessage publishResponse = await client.PostAsJsonAsync(
                                requestUri: publishUrl,
                                value: buildPayload(correlationId),
                                options: DaprNodeRequestSender.JSON_OPTIONS,
                                cancellationToken: timeoutCts.Token);

                        if (!publishResponse.IsSuccessStatusCode)
                        {
                                return null;
                        }

                        return await tcs.Task.WaitAsync(cancellationToken: timeoutCts.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                        // WHY: A 5-second timeout means no healthy/owning node responded in time; return null so callers map to 404 or 503.
                        return null;
                }
                catch (HttpRequestException)
                {
                        // WHY: When running without a Dapr sidecar (e.g., standalone local/test runs), treat Pub/Sub as unavailable rather than crashing.
                        return null;
                }
                finally
                {
                        this._replyRegistry.Remove(correlationId: correlationId);
                }
        }
}
