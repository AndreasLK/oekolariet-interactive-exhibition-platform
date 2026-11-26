using ContentService.Sdk.Common;
using ContentService.Sdk.Models;
using ContentService.Sdk.Options;
using ContentService.Sdk.Protocol;
using Microsoft.Extensions.Options;
using Refit;

namespace ContentService.Sdk.Clients;

/// <summary>
/// Thin public wrapper around the internal Refit <see cref="IContentServiceTemplateEndpoints"/> client
/// so consumers interact only with clean <c>ContentService.Sdk</c> contracts and exceptions.
/// </summary>
/// <remarks>
/// Mirrors <see cref="ContentServiceImageClient"/>: keeps Refit <see langword="internal"/>, lazily initializes
/// the proxy on first use, and translates <c>ApiException</c> into <c>ContentServiceApiException</c>.
/// </remarks>
public sealed class ContentServiceTemplateClient : IContentServiceTemplateClient
{
        private readonly ContentServiceClientOptions _options;
        private readonly Lazy<IContentServiceTemplateEndpoints> _endpoints;

        /// <summary>
        /// Initializes a new instance of <see cref="ContentServiceTemplateClient"/> using a caller-supplied <see cref="HttpClient"/>.
        /// </summary>
        /// <param name="httpClient">The configured HTTP client targeting ContentService.</param>
        /// <param name="options">Client configuration supplying optional default actor attribution, API version, or fallback base URI.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpClient"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
        public ContentServiceTemplateClient(
                HttpClient httpClient,
                IOptions<ContentServiceClientOptions> options)
        {
                ArgumentNullException.ThrowIfNull(httpClient);
                ArgumentNullException.ThrowIfNull(options);

                this._options = options.Value;
                // Defer RestService.For until the first HTTP call so unconfigured test clients can be constructed safely.
                this._endpoints = new Lazy<IContentServiceTemplateEndpoints>(() =>
                        RestService.For<IContentServiceTemplateEndpoints>(
                                client: ContentServiceEndpointUriResolver.EnsureNormalizedBaseAddress(
                                        httpClient: httpClient,
                                        options: this._options),
                                settings: RefitSettingsFactory.Create()));
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ContentServiceTemplateClient"/> from a DI-managed Refit endpoint proxy.
        /// </summary>
        /// <param name="endpoints">The DI-registered Refit endpoint proxy.</param>
        /// <param name="options">Client configuration supplying default actor attribution and API version.</param>
        internal ContentServiceTemplateClient(
                IContentServiceTemplateEndpoints endpoints,
                IOptions<ContentServiceClientOptions> options)
        {
                ArgumentNullException.ThrowIfNull(endpoints);
                ArgumentNullException.ThrowIfNull(options);

                this._options = options.Value;
                this._endpoints = new Lazy<IContentServiceTemplateEndpoints>(() => endpoints);
        }

        /// <inheritdoc />
        public async Task<ContentTemplateResponse> CreateTemplateAsync(
                ContentCreateTemplateRequest command,
                CancellationToken cancellationToken = default)
        {
                ArgumentNullException.ThrowIfNull(argument: command);

                string? validActorId = ActorHeaderResolver.ResolveValidActorId(
                        explicitActorId: command.ActorId,
                        defaultActorId: this._options.DefaultActorId);
                string apiVersion = ContentServiceEndpointUriResolver.ResolveApiVersion(options: this._options);

                try
                {
                        return await this._endpoints.Value.CreateTemplateAsync(
                                apiVersion: apiVersion,
                                command: command,
                                actorId: validActorId,
                                cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                catch (ApiException exception)
                {
                        throw RefitExceptionMapper.ToContentServiceApiException(apiException: exception);
                }
        }

        /// <inheritdoc />
        public Task<ContentPagedResponse<ContentTemplateResponse>> ListTemplatesAsync(
                int page = SdkConstants.DEFAULT_PAGE,
                int pageSize = SdkConstants.DEFAULT_PAGE_SIZE,
                ContentTemplateComponentType? componentType = null,
                string? searchTerm = null,
                CancellationToken cancellationToken = default) =>
                this.ListTemplatesAsync(
                        query: new ContentTemplateQuery
                        {
                                Page = page,
                                PageSize = pageSize,
                                ComponentType = componentType,
                                SearchTerm = searchTerm
                        },
                        cancellationToken: cancellationToken);

        /// <inheritdoc />
        public async Task<ContentPagedResponse<ContentTemplateResponse>> ListTemplatesAsync(
                ContentTemplateQuery query,
                CancellationToken cancellationToken = default)
        {
                ArgumentNullException.ThrowIfNull(argument: query);

                string? trimmedSearch = string.IsNullOrWhiteSpace(query.SearchTerm) ? null : query.SearchTerm.Trim();
                string apiVersion = ContentServiceEndpointUriResolver.ResolveApiVersion(options: this._options);

                try
                {
                        return await this._endpoints.Value.ListTemplatesAsync(
                                apiVersion: apiVersion,
                                page: query.Page,
                                pageSize: query.PageSize,
                                componentType: query.ComponentType,
                                searchTerm: trimmedSearch,
                                cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                catch (ApiException exception)
                {
                        throw RefitExceptionMapper.ToContentServiceApiException(apiException: exception);
                }
        }

        /// <inheritdoc />
        public async Task<ContentTemplateResponse> GetTemplateAsync(
                Guid templateId,
                CancellationToken cancellationToken = default)
        {
                ContentServiceEndpointUriResolver.ValidateGuid(id: templateId, paramName: nameof(templateId));
                string apiVersion = ContentServiceEndpointUriResolver.ResolveApiVersion(options: this._options);

                try
                {
                        return await this._endpoints.Value.GetTemplateAsync(
                                apiVersion: apiVersion,
                                templateId: templateId,
                                cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                catch (ApiException exception)
                {
                        throw RefitExceptionMapper.ToContentServiceApiException(apiException: exception);
                }
        }

        /// <inheritdoc />
        public async Task<ContentTemplateResponse> UpdateTemplateAsync(
                Guid templateId,
                ContentUpdateTemplateRequest command,
                CancellationToken cancellationToken = default)
        {
                ArgumentNullException.ThrowIfNull(argument: command);
                ContentServiceEndpointUriResolver.ValidateGuid(id: templateId, paramName: nameof(templateId));

                string? validActorId = ActorHeaderResolver.ResolveValidActorId(
                        explicitActorId: command.ActorId,
                        defaultActorId: this._options.DefaultActorId);
                string apiVersion = ContentServiceEndpointUriResolver.ResolveApiVersion(options: this._options);

                try
                {
                        return await this._endpoints.Value.UpdateTemplateAsync(
                                apiVersion: apiVersion,
                                templateId: templateId,
                                command: command,
                                actorId: validActorId,
                                cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                catch (ApiException exception)
                {
                        throw RefitExceptionMapper.ToContentServiceApiException(apiException: exception);
                }
        }

        /// <inheritdoc />
        public async Task DeleteTemplateAsync(
                Guid templateId,
                string? actorId = null,
                CancellationToken cancellationToken = default)
        {
                ContentServiceEndpointUriResolver.ValidateGuid(id: templateId, paramName: nameof(templateId));
                string? validActorId = ActorHeaderResolver.ResolveValidActorId(
                        explicitActorId: actorId,
                        defaultActorId: this._options.DefaultActorId);
                string apiVersion = ContentServiceEndpointUriResolver.ResolveApiVersion(options: this._options);

                try
                {
                        await this._endpoints.Value.DeleteTemplateAsync(
                                apiVersion: apiVersion,
                                templateId: templateId,
                                actorId: validActorId,
                                cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                catch (ApiException exception)
                {
                        throw RefitExceptionMapper.ToContentServiceApiException(apiException: exception);
                }
        }

        /// <inheritdoc />
        public async Task<ContentImageTemplateReferenceResponse> IsImageReferencedByTemplateAsync(
                Guid imageId,
                CancellationToken cancellationToken = default)
        {
                ContentServiceEndpointUriResolver.ValidateGuid(id: imageId, paramName: nameof(imageId));
                string apiVersion = ContentServiceEndpointUriResolver.ResolveApiVersion(options: this._options);

                try
                {
                        return await this._endpoints.Value.IsImageReferencedByTemplateAsync(
                                apiVersion: apiVersion,
                                imageId: imageId,
                                cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                catch (ApiException exception)
                {
                        throw RefitExceptionMapper.ToContentServiceApiException(apiException: exception);
                }
        }
}
