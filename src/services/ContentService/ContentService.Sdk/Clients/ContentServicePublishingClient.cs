using System.Net.Http.Json;
using ContentService.Sdk.Common;
using ContentService.Sdk.Models;
using ContentService.Sdk.Options;
using ContentService.Sdk.Protocol;
using Microsoft.Extensions.Options;

namespace ContentService.Sdk.Clients;

/// <summary>
/// Default <see cref="IContentServicePublishingClient"/> implementation backed by <see cref="HttpClient"/>.
/// </summary>
public sealed class ContentServicePublishingClient : IContentServicePublishingClient
{
        /// <summary>
        /// Typed HTTP client used to issue publish bundle requests to ContentService.
        /// </summary>
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Resolved SDK client options supplying fallback base address settings.
        /// </summary>
        private readonly ContentServiceClientOptions _options;

        /// <summary>
        /// Initializes a new instance of <see cref="ContentServicePublishingClient"/>.
        /// </summary>
        /// <param name="httpClient">The configured typed <see cref="HttpClient"/>.</param>
        /// <param name="options">Resolved <see cref="ContentServiceClientOptions"/>.</param>
        public ContentServicePublishingClient(
                HttpClient httpClient,
                IOptions<ContentServiceClientOptions> options)
        {
                ArgumentNullException.ThrowIfNull(httpClient);
                ArgumentNullException.ThrowIfNull(options);

                this._httpClient = httpClient;
                this._options = options.Value;
        }

        /// <inheritdoc />
        public async Task<ContentPublishBundle> ResolvePublishBundleAsync(
                ResolvePublishBundleRequest request,
                CancellationToken cancellationToken = default)
        {
                ArgumentNullException.ThrowIfNull(request);

                Uri requestUri = ContentServiceEndpointUriResolver.BuildPublishBundleResolveUri(
                        httpClient: this._httpClient,
                        options: this._options);

                // Serialize with SdkConstants.JSON_SERIALIZER_OPTIONS so enum and property casing match ContentService.Api.
                using JsonContent content = JsonContent.Create(
                        inputValue: request,
                        options: SdkConstants.JSON_SERIALIZER_OPTIONS);
                using HttpRequestMessage httpRequest = new(method: HttpMethod.Post, requestUri: requestUri)
                {
                        Content = content
                };
                using HttpResponseMessage response = await this._httpClient.SendAsync(
                        request: httpRequest,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                await HttpResponseErrorMapper.EnsureSuccessOrThrowAsync(
                        response: response,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                ContentPublishBundle? bundle = await response.Content.ReadFromJsonAsync<ContentPublishBundle>(
                        options: SdkConstants.JSON_SERIALIZER_OPTIONS,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                return bundle ?? throw new InvalidOperationException("ContentService returned an empty publish bundle payload.");
        }
}

