using System.Net.Http.Json;
using ContentService.Sdk.Common;
using ContentService.Sdk.Models;
using ContentService.Sdk.Options;
using ContentService.Sdk.Protocol;
using Microsoft.Extensions.Options;

namespace ContentService.Sdk.Clients;

/// <summary>
/// Default <see cref="IContentServiceHealthClient"/> implementation backed by <see cref="HttpClient"/>.
/// </summary>
public sealed class ContentServiceHealthClient : IContentServiceHealthClient
{
        /// <summary>
        /// Typed HTTP client used to query storage health telemetry from ContentService.
        /// </summary>
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Resolved SDK client options supplying fallback base address settings.
        /// </summary>
        private readonly ContentServiceClientOptions _options;

        /// <summary>
        /// Initializes a new instance of <see cref="ContentServiceHealthClient"/>.
        /// </summary>
        /// <param name="httpClient">The configured typed <see cref="HttpClient"/>.</param>
        /// <param name="options">Resolved <see cref="ContentServiceClientOptions"/>.</param>
        public ContentServiceHealthClient(
                HttpClient httpClient,
                IOptions<ContentServiceClientOptions> options)
        {
                ArgumentNullException.ThrowIfNull(httpClient);
                ArgumentNullException.ThrowIfNull(options);

                this._httpClient = httpClient;
                this._options = options.Value;
        }

        /// <inheritdoc />
        public async Task<ContentStorageHealthReport> GetStorageHealthAsync(CancellationToken cancellationToken = default)
        {
                Uri requestUri = ContentServiceEndpointUriResolver.BuildStorageHealthUri(
                        httpClient: this._httpClient,
                        options: this._options);

                using HttpRequestMessage httpRequest = new(method: HttpMethod.Get, requestUri: requestUri);
                using HttpResponseMessage response = await this._httpClient.SendAsync(
                        request: httpRequest,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                await HttpResponseErrorMapper.EnsureSuccessOrThrowAsync(
                        response: response,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                ContentStorageHealthReport? report = await response.Content.ReadFromJsonAsync<ContentStorageHealthReport>(
                        options: SdkConstants.JSON_SERIALIZER_OPTIONS,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                return report ?? throw new InvalidOperationException("ContentService returned an empty storage health payload.");
        }
}

