using System.Net.Http.Json;
using ContentService.Sdk.Common;
using ContentService.Sdk.Models;
using ContentService.Sdk.Options;
using ContentService.Sdk.Protocol;
using Microsoft.Extensions.Options;

namespace ContentService.Sdk.Clients;

/// <summary>
/// Typed <see cref="HttpClient"/> client communicating with <c>ContentService.Api</c> template endpoints.
/// </summary>
public sealed class ContentServiceTemplateClient : IContentServiceTemplateClient
{
        private readonly HttpClient _httpClient;
        private readonly ContentServiceClientOptions _options;

        /// <summary>
        /// Initializes a new instance of <see cref="ContentServiceTemplateClient"/>.
        /// </summary>
        /// <param name="httpClient">The configured HTTP client targeting ContentService.</param>
        /// <param name="options">Client configuration supplying optional default actor attribution or fallback base URI.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpClient"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
        public ContentServiceTemplateClient(
                HttpClient httpClient,
                IOptions<ContentServiceClientOptions> options)
        {
                ArgumentNullException.ThrowIfNull(httpClient);
                ArgumentNullException.ThrowIfNull(options);

                this._httpClient = httpClient;
                this._options = options.Value;
        }

        /// <inheritdoc />
        public Task<ContentPagedResult<ContentTemplateMetadata>> ListTemplatesAsync(
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
        public async Task<ContentPagedResult<ContentTemplateMetadata>> ListTemplatesAsync(
                ContentTemplateQuery query,
                CancellationToken cancellationToken = default)
        {
                ArgumentNullException.ThrowIfNull(argument: query);

                Uri requestUri = ContentServiceEndpointUriResolver.BuildTemplatesListUri(
                        httpClient: this._httpClient,
                        options: this._options,
                        query: query);

                ContentPagedResult<ContentTemplateMetadata>? pagedResult = await this.SendAndReadJsonAsync<ContentPagedResult<ContentTemplateMetadata>>(
                        method: HttpMethod.Get,
                        requestUri: requestUri,
                        content: null,
                        actorId: null,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                return pagedResult ?? throw new InvalidOperationException("ContentService returned an empty template list payload.");
        }

        /// <inheritdoc />
        public async Task<ContentTemplateMetadata> GetTemplateAsync(
                Guid templateId,
                CancellationToken cancellationToken = default)
        {
                Uri requestUri = ContentServiceEndpointUriResolver.BuildTemplateByIdUri(
                        httpClient: this._httpClient,
                        options: this._options,
                        templateId: templateId);

                ContentTemplateMetadata? metadata = await this.SendAndReadJsonAsync<ContentTemplateMetadata>(
                        method: HttpMethod.Get,
                        requestUri: requestUri,
                        content: null,
                        actorId: null,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                return metadata ?? throw new InvalidOperationException("ContentService returned an empty template payload.");
        }

        /// <inheritdoc />
        public async Task<ContentTemplateMetadata> CreateTemplateAsync(
                CreateContentTemplateRequest request,
                CancellationToken cancellationToken = default)
        {
                ArgumentNullException.ThrowIfNull(argument: request);

                // Validate actor header before issuing the POST so non-ASCII characters fail fast on the client.
                string? validActorId = ActorHeaderResolver.ResolveValidActorId(
                        explicitActorId: request.ActorId,
                        defaultActorId: this._options.DefaultActorId);
                Uri requestUri = ContentServiceEndpointUriResolver.BuildTemplatesRootUri(
                        httpClient: this._httpClient,
                        options: this._options);

                using JsonContent jsonContent = JsonContent.Create(inputValue: request, options: SdkConstants.JSON_SERIALIZER_OPTIONS);
                ContentTemplateMetadata? metadata = await this.SendAndReadJsonAsync<ContentTemplateMetadata>(
                        method: HttpMethod.Post,
                        requestUri: requestUri,
                        content: jsonContent,
                        actorId: validActorId,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                return metadata ?? throw new InvalidOperationException("ContentService returned an empty template payload.");
        }

        /// <inheritdoc />
        public async Task<ContentTemplateMetadata> UpdateTemplateAsync(
                Guid templateId,
                UpdateContentTemplateRequest request,
                CancellationToken cancellationToken = default)
        {
                ArgumentNullException.ThrowIfNull(argument: request);

                // Validate actor header before issuing the PUT so non-ASCII characters fail fast on the client.
                string? validActorId = ActorHeaderResolver.ResolveValidActorId(
                        explicitActorId: request.ActorId,
                        defaultActorId: this._options.DefaultActorId);
                Uri requestUri = ContentServiceEndpointUriResolver.BuildTemplateByIdUri(
                        httpClient: this._httpClient,
                        options: this._options,
                        templateId: templateId);

                // Serializes ExpectedConcurrencyStamp alongside template data so ContentService can reject stale edits with 409 Conflict.
                using JsonContent jsonContent = JsonContent.Create(inputValue: request, options: SdkConstants.JSON_SERIALIZER_OPTIONS);
                ContentTemplateMetadata? metadata = await this.SendAndReadJsonAsync<ContentTemplateMetadata>(
                        method: HttpMethod.Put,
                        requestUri: requestUri,
                        content: jsonContent,
                        actorId: validActorId,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                return metadata ?? throw new InvalidOperationException("ContentService returned an empty template payload.");
        }

        /// <inheritdoc />
        public async Task DeleteTemplateAsync(
                Guid templateId,
                string? actorId = null,
                CancellationToken cancellationToken = default)
        {
                string? validActorId = ActorHeaderResolver.ResolveValidActorId(
                        explicitActorId: actorId,
                        defaultActorId: this._options.DefaultActorId);
                Uri requestUri = ContentServiceEndpointUriResolver.BuildTemplateByIdUri(
                        httpClient: this._httpClient,
                        options: this._options,
                        templateId: templateId);

                using HttpResponseMessage response = await this.SendAndEnsureSuccessAsync(
                        method: HttpMethod.Delete,
                        requestUri: requestUri,
                        content: null,
                        actorId: validActorId,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends an HTTP request, maps non-success status codes to <see cref="Exceptions.ContentServiceApiException"/>, and deserializes the JSON payload.
        /// </summary>
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

        /// <summary>
        /// Sends an HTTP request with optional <c>X-Actor-Id</c> attribution and throws a mapped API exception when non-successful.
        /// </summary>
        private async Task<HttpResponseMessage> SendAndEnsureSuccessAsync(
                HttpMethod method,
                Uri requestUri,
                HttpContent? content,
                string? actorId,
                CancellationToken cancellationToken)
        {
                using HttpRequestMessage httpRequest = new(method: method, requestUri: requestUri)
                {
                        Content = content
                };

                if (actorId is not null)
                {
                        httpRequest.Headers.Add(name: SdkConstants.ACTOR_HEADER_NAME, value: actorId);
                }

                // ResponseHeadersRead lets EnsureSuccessOrThrowAsync inspect error status codes without pre-buffering large payloads.
                HttpResponseMessage response = await this._httpClient.SendAsync(
                        request: httpRequest,
                        completionOption: HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                try
                {
                        await HttpResponseErrorMapper.EnsureSuccessOrThrowAsync(
                                response: response,
                                cancellationToken: cancellationToken).ConfigureAwait(false);
                        return response;
                }
                catch
                {
                        response.Dispose();
                        throw;
                }
        }
}
