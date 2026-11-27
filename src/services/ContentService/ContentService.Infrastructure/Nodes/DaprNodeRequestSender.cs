using System.Text.Json;
using System.Text.Json.Serialization;

namespace ContentService.Infrastructure.Nodes;

/// <summary>
/// Builds Dapr sidecar invocation URIs, attaches loop-prevention and actor headers, and dispatches pooled HTTP requests to peer nodes.
/// </summary>
internal sealed class DaprNodeRequestSender
{
        /// <summary>
        /// Named <see cref="HttpClient"/> key configured with pooling for Dapr sidecar communication.
        /// </summary>
        public const string DAPR_HTTP_CLIENT_NAME = "DaprStorageNodeClient";

        /// <summary>
        /// Environment variable injected by Dapr sidecars specifying the local HTTP port.
        /// </summary>
        private const string DAPR_HTTP_PORT_ENV_VAR = "DAPR_HTTP_PORT";

        /// <summary>
        /// Standard default Dapr sidecar HTTP port when <see cref="DAPR_HTTP_PORT_ENV_VAR"/> is unset.
        /// </summary>
        private const string DEFAULT_DAPR_HTTP_PORT = "3500";

        /// <summary>
        /// Dapr app-id prefix shared by all ContentService node instances (<c>contentservice-{storageNodeId}</c>).
        /// </summary>
        private const string DAPR_APP_ID_PREFIX = "contentservice-";

        /// <summary>
        /// Loop-prevention header attached to all outbound peer-node requests.
        /// </summary>
        private const string NODE_FORWARDED_HEADER_NAME = "X-Node-Forwarded";

        /// <summary>
        /// Header value marking a request as already forwarded across storage nodes.
        /// </summary>
        private const string NODE_FORWARDED_HEADER_VALUE = "true";

        /// <summary>
        /// Actor attribution header forwarded on mutating peer requests.
        /// </summary>
        private const string ACTOR_HEADER_NAME = "X-Actor-Id";

        /// <summary>
        /// JSON options matching <c>ContentService.Api</c> enum-as-string serialization.
        /// </summary>
        public static readonly JsonSerializerOptions JSON_OPTIONS = CreateJsonOptions();

        /// <summary>
        /// Pooled HTTP client factory used for Dapr sidecar requests.
        /// </summary>
        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>
        /// Base Dapr HTTP sidecar address (e.g., <c>http://127.0.0.1:3500</c>).
        /// </summary>
        private readonly string _daprBaseUrl;

        /// <summary>
        /// Initializes a new instance of the <see cref="DaprNodeRequestSender"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The pooled HTTP client factory.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpClientFactory"/> is <see langword="null"/>.</exception>
        public DaprNodeRequestSender(IHttpClientFactory httpClientFactory)
        {
                ArgumentNullException.ThrowIfNull(httpClientFactory);
                this._httpClientFactory = httpClientFactory;

                // Resolve the sidecar port once at construction so every outbound peer call shares the same base URL.
                string daprPort = Environment.GetEnvironmentVariable(DAPR_HTTP_PORT_ENV_VAR) ?? DEFAULT_DAPR_HTTP_PORT;
                this._daprBaseUrl = $"http://127.0.0.1:{daprPort.Trim()}";
        }

        /// <summary>
        /// Builds the Dapr HTTP service invocation URI for <c>contentservice-{storageNodeId}</c>.
        /// </summary>
        /// <param name="storageNodeId">The target storage node identifier.</param>
        /// <param name="relativeApiPath">The relative API route on the target node.</param>
        /// <returns>An absolute Dapr service invocation <see cref="Uri"/>.</returns>
        public Uri BuildDaprInvokeUri(string storageNodeId, string relativeApiPath)
        {
                string normalizedNodeId = storageNodeId.Trim().ToLowerInvariant();
                string appId = $"{DAPR_APP_ID_PREFIX}{normalizedNodeId}";
                return new Uri(
                        uriString: $"{this._daprBaseUrl}/v1.0/invoke/{appId}/method/{relativeApiPath}",
                        uriKind: UriKind.Absolute);
        }

        /// <summary>
        /// Creates an outbound <see cref="HttpRequestMessage"/> tagged with <c>X-Node-Forwarded</c> to prevent multi-hop loops.
        /// </summary>
        /// <param name="method">The HTTP method to send.</param>
        /// <param name="uri">The target Dapr invocation URI.</param>
        /// <param name="actorId">Optional actor identifier forwarded on mutating requests.</param>
        /// <returns>The configured <see cref="HttpRequestMessage"/>.</returns>
        public HttpRequestMessage CreateRequest(
                HttpMethod method,
                Uri uri,
                string? actorId = null)
        {
                HttpRequestMessage request = new(method: method, requestUri: uri);
                request.Headers.TryAddWithoutValidation(name: NODE_FORWARDED_HEADER_NAME, value: NODE_FORWARDED_HEADER_VALUE);

                if (!string.IsNullOrWhiteSpace(actorId))
                {
                        request.Headers.TryAddWithoutValidation(name: ACTOR_HEADER_NAME, value: actorId);
                }

                return request;
        }

        /// <summary>
        /// Dispatches the request through the pooled Dapr <see cref="HttpClient"/>.
        /// </summary>
        /// <param name="request">The outbound HTTP request message.</param>
        /// <param name="completionOption">Controls whether response headers or full content are buffered before returning.</param>
        /// <param name="cancellationToken">Propagates cancellation to the underlying HTTP send.</param>
        /// <returns>The peer node's <see cref="HttpResponseMessage"/>.</returns>
        public Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                HttpCompletionOption completionOption,
                CancellationToken cancellationToken)
        {
                HttpClient client = this._httpClientFactory.CreateClient(DAPR_HTTP_CLIENT_NAME);
                return client.SendAsync(
                        request: request,
                        completionOption: completionOption,
                        cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Builds shared JSON serializer options with string enum support.
        /// </summary>
        /// <returns>A configured <see cref="JsonSerializerOptions"/> instance.</returns>
        private static JsonSerializerOptions CreateJsonOptions()
        {
                JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
                options.Converters.Add(new JsonStringEnumConverter());
                return options;
        }
}

