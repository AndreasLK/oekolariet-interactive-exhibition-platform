using System.Net.Http.Json;
using ContentService.Sdk.Common;
using ContentService.Sdk.Models;
using ContentService.Sdk.Options;
using ContentService.Sdk.Protocol;
using Microsoft.Extensions.Options;

namespace ContentService.Sdk.Clients;

/// <summary>
/// Typed HTTP client implementing <see cref="IContentServiceTemplateClient"/> against ContentService's <c>/api/templates</c> endpoints.
/// </summary>
public sealed class ContentServiceTemplateClient : IContentServiceTemplateClient
{
        private readonly HttpClient _httpClient;
        private readonly ContentServiceClientOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContentServiceTemplateClient"/> class.
        /// </summary>
        /// <param name="httpClient">The configured <see cref="HttpClient"/> instance.</param>
        /// <param name="options">Client options supplying fallback base address and default actor identity.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpClient"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
        public ContentServiceTemplateClient(
                HttpClient httpClient,
                IOptions<ContentServiceClientOptions> options)
        {
                ArgumentNullException.ThrowIfNull(argument: httpClient);
                ArgumentNullException.ThrowIfNull(argument: options);
                ArgumentNullException.ThrowIfNull(argument: options.Value);

                this._httpClient = httpClient;
                this._options = options.Value;
        }

        /// <inheritdoc />
        public async Task<ContentPagedResult<ContentTemplateMetadata>> ListTemplatesAsync(
                ContentTemplateQuery? query = null,
                CancellationToken cancellationToken = default)
        {
                ContentTemplateQuery effectiveQuery = query ?? new ContentTemplateQuery();
                Uri requestUri = ContentServiceEndpointUriResolver.BuildTemplatesListUri(
                        httpClient: this._httpClient,
                        options: this._options,
                        query: effectiveQuery);

                ContentPagedResult<ContentTemplateMetadata>? result = await this.SendAndReadJsonAsync<ContentPagedResult<ContentTemplateMetadata>>(
                        method: HttpMethod.Get,
                        requestUri: requestUri,
                        content: null,
                        actorId: null,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                return result ?? new ContentPagedResult<ContentTemplateMetadata>
                {
                        Items = [],
                        Page = effectiveQuery.Page,
                        PageSize = effectiveQuery.PageSize,
                        TotalCount = 0
                };
        }

        /// <inheritdoc />
        public async Task<ContentTemplateMetadata> GetTemplateAsync(
                Guid templateId,
                CancellationToken cancellationToken = default)
        {
                Uri requestUri = ContentServiceEndpointUriResolver.BuildTemplateItemUri(
                        httpClient: this._httpClient,
                        options: this._options,
                        templateId: templateId);

                ContentTemplateMetadata? template = await this.SendAndReadJsonAsync<ContentTemplateMetadata>(
                        method: HttpMethod.Get,
                        requestUri: requestUri,
                        content: null,
                        actorId: null,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                return template ?? throw new InvalidOperationException("ContentService returned an empty template payload.");
        }

        /// <inheritdoc />
        public async Task<ContentTemplateMetadata> CreateTemplateAsync(
                CreateContentTemplateRequest request,
                CancellationToken cancellationToken = default)
        {
                ArgumentNullException.ThrowIfNull(argument: request);

                string? actorId = ActorHeaderResolver.ResolveValidActorId(
                        explicitActorId: request.ActorId,
                        defaultActorId: this._options.DefaultActorId);
                Uri requestUri = ContentServiceEndpointUriResolver.BuildTemplatesRootUri(
                        httpClient: this._httpClient,
                        options: this._options);

                using JsonContent content = JsonContent.Create(
                        inputValue: request with { ActorId = actorId },
                        options: SdkConstants.JSON_SERIALIZER_OPTIONS);

                ContentTemplateMetadata? created = await this.SendAndReadJsonAsync<ContentTemplateMetadata>(
                        method: HttpMethod.Post,
                        requestUri: requestUri,
                        content: content,
                        actorId: actorId,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                return created ?? throw new InvalidOperationException("ContentService returned an empty template creation payload.");
        }

        /// <inheritdoc />
        public async Task<ContentTemplateMetadata> UpdateTemplateAsync(
                Guid templateId,
                UpdateContentTemplateRequest request,
                CancellationToken cancellationToken = default)
        {
                ArgumentNullException.ThrowIfNull(argument: request);

                string? actorId = ActorHeaderResolver.ResolveValidActorId(
                        explicitActorId: request.ActorId,
                        defaultActorId: this._options.DefaultActorId);
                Uri requestUri = ContentServiceEndpointUriResolver.BuildTemplateItemUri(
                        httpClient: this._httpClient,
                        options: this._options,
                        templateId: templateId);

                using JsonContent content = JsonContent.Create(
                        inputValue: request with { ActorId = actorId },
                        options: SdkConstants.JSON_SERIALIZER_OPTIONS);

                ContentTemplateMetadata? updated = await this.SendAndReadJsonAsync<ContentTemplateMetadata>(
                        method: HttpMethod.Put,
                        requestUri: requestUri,
                        content: content,
                        actorId: actorId,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                return updated ?? throw new InvalidOperationException("ContentService returned an empty template update payload.");
        }

        /// <inheritdoc />
        public async Task DeleteTemplateAsync(
                Guid templateId,
                string? actorId = null,
                CancellationToken cancellationToken = default)
        {
                string? effectiveActorId = ActorHeaderResolver.ResolveValidActorId(
                        explicitActorId: actorId,
                        defaultActorId: this._options.DefaultActorId);
                Uri requestUri = ContentServiceEndpointUriResolver.BuildTemplateItemUri(
                        httpClient: this._httpClient,
                        options: this._options,
                        templateId: templateId);

                using HttpResponseMessage response = await this.SendAndEnsureSuccessAsync(
                        method: HttpMethod.Delete,
                        requestUri: requestUri,
                        content: null,
                        actorId: effectiveActorId,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        private async Task<TResponse?> SendAndReadJsonAsync<TResponse>(
                HttpMethod method,
                Uri requestUri,
                HttpContent? content,
                string? actorId,
                CancellationToken cancellationToken)
        {
                using HttpResponseMessage response = await this.SendAndEnsureSuccessAsync(
                        method: method,
                        requestUri: requestUri,
                        content: content,
                        actorId: actorId,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                return await response.Content.ReadFromJsonAsync<TResponse>(
                        options: SdkConstants.JSON_SERIALIZER_OPTIONS,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        private async Task<HttpResponseMessage> SendAndEnsureSuccessAsync(
                HttpMethod method,
                Uri requestUri,
                HttpContent? content,
                string? actorId,
                CancellationToken cancellationToken)
        {
                using HttpRequestMessage request = new(method: method, requestUri: requestUri);
                request.Content = content;

                if (!string.IsNullOrWhiteSpace(value: actorId))
                {
                        request.Headers.TryAddWithoutValidation(
                                name: SdkConstants.ACTOR_HEADER_NAME,
                                value: actorId);
                }

                HttpResponseMessage response = await this._httpClient
                        .SendAsync(request: request, completionOption: HttpCompletionOption.ResponseHeadersRead, cancellationToken: cancellationToken)
                        .ConfigureAwait(false);

                try
                {
                        await HttpResponseErrorMapper
                                .EnsureSuccessOrThrowAsync(response: response, cancellationToken: cancellationToken)
                                .ConfigureAwait(false);
                        return response;
                }
                catch
                {
                        response.Dispose();
                        throw;
                }
        }
}
