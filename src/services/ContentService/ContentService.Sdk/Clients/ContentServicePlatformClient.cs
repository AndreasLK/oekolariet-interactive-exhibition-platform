using ContentService.Sdk.Models;
using ContentService.Sdk.Options;
using ContentService.Sdk.Protocol;
using Microsoft.Extensions.Options;
using Refit;

namespace ContentService.Sdk.Clients;

/// <summary>
/// Thin public wrapper around the internal Refit <see cref="IContentServicePlatformEndpoints"/> client
/// for publishing bundle resolution, storage health telemetry, and pool upload-offer selection.
/// </summary>
/// <remarks>
/// Mirrors <see cref="ContentServiceImageClient"/>: shields consumers from Refit types and normalizes
/// HTTP failures into <c>ContentServiceApiException</c>.
/// </remarks>
public sealed class ContentServicePlatformClient : IContentServicePlatformClient
{
        private readonly ContentServiceClientOptions _options;
        private readonly Lazy<IContentServicePlatformEndpoints> _endpoints;

        /// <summary>
        /// Initializes a new instance of <see cref="ContentServicePlatformClient"/> using a caller-supplied <see cref="HttpClient"/>.
        /// </summary>
        /// <param name="httpClient">The configured HTTP client targeting ContentService.</param>
        /// <param name="options">Client configuration supplying API version or fallback base URI settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpClient"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
        public ContentServicePlatformClient(
                HttpClient httpClient,
                IOptions<ContentServiceClientOptions> options)
        {
                ArgumentNullException.ThrowIfNull(httpClient);
                ArgumentNullException.ThrowIfNull(options);

                this._options = options.Value;
                // Defer RestService.For until the first HTTP call so unconfigured test clients can be constructed safely.
                this._endpoints = new Lazy<IContentServicePlatformEndpoints>(() =>
                        RestService.For<IContentServicePlatformEndpoints>(
                                client: ContentServiceEndpointUriResolver.EnsureNormalizedBaseAddress(
                                        httpClient: httpClient,
                                        options: this._options),
                                settings: RefitSettingsFactory.Create()));
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ContentServicePlatformClient"/> from a DI-managed Refit endpoint proxy.
        /// </summary>
        /// <param name="endpoints">The DI-registered Refit endpoint proxy.</param>
        /// <param name="options">Client configuration supplying API version and base URI settings.</param>
        internal ContentServicePlatformClient(
                IContentServicePlatformEndpoints endpoints,
                IOptions<ContentServiceClientOptions> options)
        {
                ArgumentNullException.ThrowIfNull(endpoints);
                ArgumentNullException.ThrowIfNull(options);

                this._options = options.Value;
                this._endpoints = new Lazy<IContentServicePlatformEndpoints>(() => endpoints);
        }

        /// <inheritdoc />
        public async Task<ContentPublishBundleResponse> ResolvePublishBundleAsync(
                ContentResolvePublishBundleQuery query,
                CancellationToken cancellationToken = default)
        {
                ArgumentNullException.ThrowIfNull(argument: query);
                string apiVersion = ContentServiceEndpointUriResolver.ResolveApiVersion(options: this._options);

                try
                {
                        return await this._endpoints.Value.ResolvePublishBundleAsync(
                                apiVersion: apiVersion,
                                query: query,
                                cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                catch (ApiException exception)
                {
                        throw RefitExceptionMapper.ToContentServiceApiException(apiException: exception);
                }
        }

        /// <inheritdoc />
        public async Task<ContentStorageHealthResponse> GetStorageHealthAsync(
                CancellationToken cancellationToken = default)
        {
                string apiVersion = ContentServiceEndpointUriResolver.ResolveApiVersion(options: this._options);

                try
                {
                        return await this._endpoints.Value.GetStorageHealthAsync(
                                apiVersion: apiVersion,
                                cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                catch (ApiException exception)
                {
                        throw RefitExceptionMapper.ToContentServiceApiException(apiException: exception);
                }
        }

        /// <inheritdoc />
        public Task<ContentPoolNodeOfferResponse> RequestUploadOfferAsync(
                long requiredBytes,
                CancellationToken cancellationToken = default) =>
                this.RequestUploadOfferAsync(
                        command: new ContentRequestUploadOfferCommand(RequiredBytes: requiredBytes),
                        cancellationToken: cancellationToken);

        /// <inheritdoc />
        public async Task<ContentPoolNodeOfferResponse> RequestUploadOfferAsync(
                ContentRequestUploadOfferCommand command,
                CancellationToken cancellationToken = default)
        {
                ArgumentNullException.ThrowIfNull(argument: command);
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value: command.RequiredBytes);

                string apiVersion = ContentServiceEndpointUriResolver.ResolveApiVersion(options: this._options);

                try
                {
                        return await this._endpoints.Value.RequestUploadOfferAsync(
                                apiVersion: apiVersion,
                                command: command,
                                cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                catch (ApiException exception)
                {
                        throw RefitExceptionMapper.ToContentServiceApiException(apiException: exception);
                }
        }
}
