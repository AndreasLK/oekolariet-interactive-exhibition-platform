using System.Net.Http.Json;
using ContentService.Sdk.Common;
using ContentService.Sdk.Models;
using ContentService.Sdk.Options;
using ContentService.Sdk.Protocol;
using Microsoft.Extensions.Options;

namespace ContentService.Sdk.Clients;

/// <summary>
/// Typed <see cref="HttpClient"/> client communicating with <c>ContentService.Api</c> image endpoints.
/// </summary>
public sealed class ContentServiceImageClient : IContentServiceImageClient
{
        private readonly HttpClient _httpClient;
        private readonly ContentServiceClientOptions _options;

        /// <summary>
        /// Initializes a new instance of <see cref="ContentServiceImageClient"/>.
        /// </summary>
        /// <param name="httpClient">The configured HTTP client targeting ContentService.</param>
        /// <param name="options">Client configuration supplying optional default actor attribution or fallback base URI.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpClient"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
        public ContentServiceImageClient(
                HttpClient httpClient,
                IOptions<ContentServiceClientOptions> options)
        {
                ArgumentNullException.ThrowIfNull(httpClient);
                ArgumentNullException.ThrowIfNull(options);

                this._httpClient = httpClient;
                this._options = options.Value;
        }

        /// <inheritdoc />
        public Task<ContentPagedResult<ContentImageMetadata>> ListImagesAsync(
                int page = SdkConstants.DEFAULT_PAGE,
                int pageSize = SdkConstants.DEFAULT_PAGE_SIZE,
                string? searchTerm = null,
                CancellationToken cancellationToken = default) =>
                this.ListImagesAsync(
                        query: new ContentImageQuery
                        {
                                Page = page,
                                PageSize = pageSize,
                                SearchTerm = searchTerm
                        },
                        cancellationToken: cancellationToken);

        /// <inheritdoc />
        public async Task<ContentPagedResult<ContentImageMetadata>> ListImagesAsync(
                ContentImageQuery query,
                CancellationToken cancellationToken = default)
        {
                ArgumentNullException.ThrowIfNull(argument: query);

                Uri requestUri = ContentServiceEndpointUriResolver.BuildImagesListUri(
                        httpClient: this._httpClient,
                        options: this._options,
                        query: query);

                ContentPagedResult<ContentImageMetadata>? pagedResult = await this.SendAndReadJsonAsync<ContentPagedResult<ContentImageMetadata>>(
                        method: HttpMethod.Get,
                        requestUri: requestUri,
                        content: null,
                        actorId: null,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                return pagedResult ?? throw new InvalidOperationException("ContentService returned an empty image list payload.");
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<string>> ListAlbumsAsync(
                string? parentPath = null,
                CancellationToken cancellationToken = default)
        {
                Uri requestUri = ContentServiceEndpointUriResolver.BuildImageAlbumsUri(
                        httpClient: this._httpClient,
                        options: this._options,
                        parentPath: parentPath);

                IReadOnlyList<string>? albums = await this.SendAndReadJsonAsync<IReadOnlyList<string>>(
                        method: HttpMethod.Get,
                        requestUri: requestUri,
                        content: null,
                        actorId: null,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                return albums ?? [];
        }

        /// <inheritdoc />
        public async Task<ContentImageMetadata> UpdateImageMetadataAsync(
                Guid imageId,
                UpdateImageMetadataRequest request,
                CancellationToken cancellationToken = default)
        {
                ArgumentNullException.ThrowIfNull(argument: request);

                // Validate actor header before issuing the PATCH so non-ASCII characters fail fast on the client instead of throwing inside HttpHeaders.
                string? validActorId = ActorHeaderResolver.ResolveValidActorId(
                        explicitActorId: request.ActorId,
                        defaultActorId: this._options.DefaultActorId);
                Uri requestUri = ContentServiceEndpointUriResolver.BuildImageMetadataUpdateUri(
                        httpClient: this._httpClient,
                        options: this._options,
                        imageId: imageId);

                // Serializes ExpectedConcurrencyStamp alongside metadata so ContentService can reject stale edits with 409 Conflict.
                using JsonContent jsonContent = JsonContent.Create(inputValue: request, options: SdkConstants.JSON_SERIALIZER_OPTIONS);
                ContentImageMetadata? metadata = await this.SendAndReadJsonAsync<ContentImageMetadata>(
                        method: HttpMethod.Patch,
                        requestUri: requestUri,
                        content: jsonContent,
                        actorId: validActorId,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                return metadata ?? throw new InvalidOperationException("ContentService returned an empty metadata payload.");
        }

        /// <inheritdoc />
        public async Task<ContentImageMetadata> UploadImageAsync(
                UploadImageRequest request,
                CancellationToken cancellationToken = default)
        {
                ArgumentNullException.ThrowIfNull(request);

                // Validate actor header before wrapping the stream so invalid headers fail without touching request.Content.
                string? validActorId = ActorHeaderResolver.ResolveValidActorId(
                        explicitActorId: request.ActorId,
                        defaultActorId: this._options.DefaultActorId);
                Uri requestUri = ContentServiceEndpointUriResolver.BuildImagesRootUri(
                        httpClient: this._httpClient,
                        options: this._options);

                using MultipartFormDataContent multipartContent = ImageUploadMultipartContentFactory.Create(request: request);
                ContentImageMetadata? metadata = await this.SendAndReadJsonAsync<ContentImageMetadata>(
                        method: HttpMethod.Post,
                        requestUri: requestUri,
                        content: multipartContent,
                        actorId: validActorId,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                return metadata ?? throw new InvalidOperationException("ContentService returned an empty metadata payload.");
        }

        /// <inheritdoc />
        public Task<ContentImageDownload> DownloadImageAsync(
                Guid imageId,
                CancellationToken cancellationToken = default)
        {
                Uri requestUri = this.GetImageContentUri(imageId: imageId);
                return this.DownloadStreamAsync(
                        requestUri: requestUri,
                        cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public Task<ContentImageDownload> DownloadImagePreviewAsync(
                Guid imageId,
                CancellationToken cancellationToken = default)
        {
                Uri requestUri = this.GetImagePreviewUri(imageId: imageId);
                return this.DownloadStreamAsync(
                        requestUri: requestUri,
                        cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Streams an image content or preview response while transferring <see cref="HttpResponseMessage"/> ownership to <see cref="ContentImageDownload"/>.
        /// </summary>
        /// <param name="requestUri">The resolved content or preview endpoint URI.</param>
        /// <param name="cancellationToken">A token to cancel the HTTP request before headers arrive.</param>
        /// <returns>A disposable <see cref="ContentImageDownload"/> owning the payload stream and response.</returns>
        private async Task<ContentImageDownload> DownloadStreamAsync(
                Uri requestUri,
                CancellationToken cancellationToken)
        {
                using HttpRequestMessage httpRequest = new(method: HttpMethod.Get, requestUri: requestUri);

                // IMPORTANT: Do NOT wrap 'response' in a 'using' block here!
                // With ResponseHeadersRead, ReadAsStreamAsync returns a network stream backed by the HttpResponseMessage.
                // Disposing 'response' before returning would immediately close the stream before the caller can read the image bytes.
                // Instead, ContentImageDownload takes ownership and disposes both stream and response when the caller disposes it;
                // the catch block below cleans them up only if an error happens before ownership is handed over.
                HttpResponseMessage response = await this._httpClient.SendAsync(
                        request: httpRequest,
                        completionOption: HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                Stream? contentStream = null;
                try
                {
                        await HttpResponseErrorMapper.EnsureSuccessOrThrowAsync(
                                response: response,
                                cancellationToken: cancellationToken).ConfigureAwait(false);

                        contentStream = await response.Content.ReadAsStreamAsync(
                                cancellationToken: cancellationToken).ConfigureAwait(false);

                        return ContentImageDownload.FromHttpResponse(
                                response: response,
                                contentStream: contentStream);
                }
                catch
                {
                        if (contentStream is not null)
                        {
                                await contentStream.DisposeAsync().ConfigureAwait(false);
                        }

                        response.Dispose();
                        throw;
                }
        }

        /// <inheritdoc />
        public async Task<ContentImageMetadata> GetImageMetadataAsync(
                Guid imageId,
                CancellationToken cancellationToken = default)
        {
                Uri requestUri = ContentServiceEndpointUriResolver.BuildImageMetadataUri(
                        httpClient: this._httpClient,
                        options: this._options,
                        imageId: imageId);

                ContentImageMetadata? metadata = await this.SendAndReadJsonAsync<ContentImageMetadata>(
                        method: HttpMethod.Get,
                        requestUri: requestUri,
                        content: null,
                        actorId: null,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                return metadata ?? throw new InvalidOperationException("ContentService returned an empty metadata payload.");
        }

        /// <inheritdoc />
        public async Task DeleteImageAsync(
                Guid imageId,
                string? actorId = null,
                CancellationToken cancellationToken = default)
        {
                string? validActorId = ActorHeaderResolver.ResolveValidActorId(
                        explicitActorId: actorId,
                        defaultActorId: this._options.DefaultActorId);
                Uri requestUri = ContentServiceEndpointUriResolver.BuildImageMetadataUri(
                        httpClient: this._httpClient,
                        options: this._options,
                        imageId: imageId);

                using HttpResponseMessage response = await this.SendAndEnsureSuccessAsync(
                        method: HttpMethod.Delete,
                        requestUri: requestUri,
                        content: null,
                        actorId: validActorId,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public Uri GetImageContentUri(Guid imageId) =>
                ContentServiceEndpointUriResolver.BuildImageContentUri(
                        httpClient: this._httpClient,
                        options: this._options,
                        imageId: imageId);

        /// <inheritdoc />
        public Uri GetImagePreviewUri(Guid imageId) =>
                ContentServiceEndpointUriResolver.BuildImagePreviewUri(
                        httpClient: this._httpClient,
                        options: this._options,
                        imageId: imageId);

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
