using ContentService.Sdk.Common;
using ContentService.Sdk.Models;
using ContentService.Sdk.Options;
using ContentService.Sdk.Protocol;
using Microsoft.Extensions.Options;
using Refit;

namespace ContentService.Sdk.Clients;

/// <summary>
/// Thin public wrapper around the internal Refit <see cref="IContentServiceImageEndpoints"/> client
/// so consumers interact only with clean <c>ContentService.Sdk</c> contracts and exceptions.
/// </summary>
/// <remarks>
/// Non-standard SDK architecture: Instead of exposing Refit's <see cref="IContentServiceImageEndpoints"/> directly,
/// this wrapper hides Refit behind <see cref="IContentServiceImageClient"/> and lazily builds the proxy via <see cref="Lazy{T}"/>.
/// <para>
/// What we gain: Callers get uniform <c>ContentServiceApiException</c> errors, client-side guard validation,
/// URI-building helpers that work without issuing HTTP calls, and safe construction in unit tests before <c>BaseAddress</c> is hit.
/// What we lose: One extra wrapper layer and explicit <see cref="HttpResponseMessage"/> ownership hand-off in <see cref="DownloadStreamCoreAsync"/>.
/// </para>
/// </remarks>
public sealed class ContentServiceImageClient : IContentServiceImageClient
{
        private readonly HttpClient? _httpClient;
        private readonly ContentServiceClientOptions _options;
        private readonly Lazy<IContentServiceImageEndpoints> _endpoints;

        /// <summary>
        /// Initializes a new instance of <see cref="ContentServiceImageClient"/> using a caller-supplied <see cref="HttpClient"/>.
        /// </summary>
        /// <param name="httpClient">The configured HTTP client targeting ContentService.</param>
        /// <param name="options">Client configuration supplying optional default actor attribution, API version, or fallback base URI.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpClient"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
        public ContentServiceImageClient(
                HttpClient httpClient,
                IOptions<ContentServiceClientOptions> options)
        {
                ArgumentNullException.ThrowIfNull(httpClient);
                ArgumentNullException.ThrowIfNull(options);

                this._httpClient = httpClient;
                this._options = options.Value;
                // Defer RestService.For until the first HTTP call so unconfigured test clients can be constructed without throwing prematurely.
                this._endpoints = new Lazy<IContentServiceImageEndpoints>(() =>
                        RestService.For<IContentServiceImageEndpoints>(
                                client: ContentServiceEndpointUriResolver.EnsureNormalizedBaseAddress(
                                        httpClient: httpClient,
                                        options: this._options),
                                settings: RefitSettingsFactory.Create()));
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ContentServiceImageClient"/> from a DI-managed Refit endpoint proxy.
        /// </summary>
        /// <param name="endpoints">The DI-registered Refit endpoint proxy.</param>
        /// <param name="options">Client configuration supplying default actor attribution and API version.</param>
        internal ContentServiceImageClient(
                IContentServiceImageEndpoints endpoints,
                IOptions<ContentServiceClientOptions> options)
        {
                ArgumentNullException.ThrowIfNull(endpoints);
                ArgumentNullException.ThrowIfNull(options);

                this._options = options.Value;
                this._endpoints = new Lazy<IContentServiceImageEndpoints>(() => endpoints);
        }

        /// <inheritdoc />
        public Task<ContentPagedResponse<ContentImageResponse>> ListImagesAsync(
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
        public async Task<ContentPagedResponse<ContentImageResponse>> ListImagesAsync(
                ContentImageQuery query,
                CancellationToken cancellationToken = default)
        {
                ArgumentNullException.ThrowIfNull(argument: query);

                // Normalize optional filters so whitespace-only strings and standalone IncludeSubalbums flags are omitted.
                string? trimmedSearch = string.IsNullOrWhiteSpace(query.SearchTerm) ? null : query.SearchTerm.Trim();
                string? trimmedAlbum = string.IsNullOrWhiteSpace(query.AlbumPath) ? null : query.AlbumPath.Trim();
                string? includeSubalbums = trimmedAlbum is null ? null : (query.IncludeSubalbums ? "true" : "false");
                string apiVersion = ContentServiceEndpointUriResolver.ResolveApiVersion(options: this._options);

                try
                {
                        return await this._endpoints.Value.ListImagesAsync(
                                apiVersion: apiVersion,
                                page: query.Page,
                                pageSize: query.PageSize,
                                searchTerm: trimmedSearch,
                                albumPath: trimmedAlbum,
                                includeSubalbums: includeSubalbums,
                                cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                catch (ApiException exception)
                {
                        throw RefitExceptionMapper.ToContentServiceApiException(apiException: exception);
                }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<string>> ListAlbumsAsync(
                string? parentPath = null,
                CancellationToken cancellationToken = default)
        {
                string? trimmedParent = string.IsNullOrWhiteSpace(parentPath) ? null : parentPath.Trim();
                string apiVersion = ContentServiceEndpointUriResolver.ResolveApiVersion(options: this._options);

                try
                {
                        IReadOnlyList<string>? albums = await this._endpoints.Value.ListAlbumsAsync(
                                apiVersion: apiVersion,
                                parentPath: trimmedParent,
                                cancellationToken: cancellationToken).ConfigureAwait(false);

                        return albums ?? [];
                }
                catch (ApiException exception)
                {
                        throw RefitExceptionMapper.ToContentServiceApiException(apiException: exception);
                }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ContentImageResponse>> GetImagesMetadataBatchAsync(
                IReadOnlyCollection<Guid> imageIds,
                CancellationToken cancellationToken = default)
        {
                ArgumentNullException.ThrowIfNull(argument: imageIds);

                // Short-circuit empty batches client-side to avoid an unnecessary HTTP round-trip.
                if (imageIds.Count == 0)
                {
                        return [];
                }

                string apiVersion = ContentServiceEndpointUriResolver.ResolveApiVersion(options: this._options);

                try
                {
                        IReadOnlyList<ContentImageResponse>? results = await this._endpoints.Value.GetImagesMetadataBatchAsync(
                                apiVersion: apiVersion,
                                imageIds: imageIds,
                                cancellationToken: cancellationToken).ConfigureAwait(false);

                        return results ?? [];
                }
                catch (ApiException exception)
                {
                        throw RefitExceptionMapper.ToContentServiceApiException(apiException: exception);
                }
        }

        /// <inheritdoc />
        public Task<ContentImageResponse> UpdateImageMetadataAsync(
                Guid imageId,
                ContentUpdateImageMetadataRequest command,
                CancellationToken cancellationToken = default)
        {
                ContentServiceEndpointUriResolver.ValidateGuid(id: imageId, paramName: nameof(imageId));
                return this.UpdateImageMetadataAsync(
                        imageIdentifier: imageId.ToString("D"),
                        command: command,
                        cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public async Task<ContentImageResponse> UpdateImageMetadataAsync(
                string imageIdentifier,
                ContentUpdateImageMetadataRequest command,
                CancellationToken cancellationToken = default)
        {
                ArgumentNullException.ThrowIfNull(argument: command);

                string normalizedId = ContentServiceEndpointUriResolver.NormalizeIdentifier(identifier: imageIdentifier, paramName: nameof(imageIdentifier));
                string? validActorId = ActorHeaderResolver.ResolveValidActorId(
                        explicitActorId: command.ActorId,
                        defaultActorId: this._options.DefaultActorId);
                string apiVersion = ContentServiceEndpointUriResolver.ResolveApiVersion(options: this._options);

                try
                {
                        return await this._endpoints.Value.UpdateImageMetadataAsync(
                                apiVersion: apiVersion,
                                imageIdentifier: normalizedId,
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
        public async Task<ContentImageResponse> UploadImageAsync(
                ContentUploadImageRequest command,
                CancellationToken cancellationToken = default)
        {
                ArgumentNullException.ThrowIfNull(command);

                // Validate actor header before wrapping the stream so invalid headers fail without touching command.Content.
                string? validActorId = ActorHeaderResolver.ResolveValidActorId(
                        explicitActorId: command.ActorId,
                        defaultActorId: this._options.DefaultActorId);
                string apiVersion = ContentServiceEndpointUriResolver.ResolveApiVersion(options: this._options);

                using MultipartFormDataContent multipartContent = ImageUploadMultipartContentFactory.Create(command: command);
                try
                {
                        return await this._endpoints.Value.UploadImageAsync(
                                apiVersion: apiVersion,
                                content: multipartContent,
                                actorId: validActorId,
                                cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                catch (ApiException exception)
                {
                        throw RefitExceptionMapper.ToContentServiceApiException(apiException: exception);
                }
        }

        /// <inheritdoc />
        public Task<ContentImageDownloadResponse> DownloadImageAsync(
                Guid imageId,
                CancellationToken cancellationToken = default)
        {
                ContentServiceEndpointUriResolver.ValidateGuid(id: imageId, paramName: nameof(imageId));
                return this.DownloadImageAsync(
                        imageIdentifier: imageId.ToString("D"),
                        cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public Task<ContentImageDownloadResponse> DownloadImageAsync(
                string imageIdentifier,
                CancellationToken cancellationToken = default)
        {
                string normalizedId = ContentServiceEndpointUriResolver.NormalizeIdentifier(identifier: imageIdentifier, paramName: nameof(imageIdentifier));
                string apiVersion = ContentServiceEndpointUriResolver.ResolveApiVersion(options: this._options);

                return DownloadStreamCoreAsync(
                        sendAsync: () => this._endpoints.Value.DownloadImageAsync(
                                apiVersion: apiVersion,
                                imageIdentifier: normalizedId,
                                cancellationToken: cancellationToken),
                        cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public Task<ContentImageDownloadResponse> DownloadImagePreviewAsync(
                Guid imageId,
                CancellationToken cancellationToken = default)
        {
                ContentServiceEndpointUriResolver.ValidateGuid(id: imageId, paramName: nameof(imageId));
                return this.DownloadImagePreviewAsync(
                        imageIdentifier: imageId.ToString("D"),
                        cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public Task<ContentImageDownloadResponse> DownloadImagePreviewAsync(
                string imageIdentifier,
                CancellationToken cancellationToken = default)
        {
                string normalizedId = ContentServiceEndpointUriResolver.NormalizeIdentifier(identifier: imageIdentifier, paramName: nameof(imageIdentifier));
                string apiVersion = ContentServiceEndpointUriResolver.ResolveApiVersion(options: this._options);

                return DownloadStreamCoreAsync(
                        sendAsync: () => this._endpoints.Value.DownloadImagePreviewAsync(
                                apiVersion: apiVersion,
                                imageIdentifier: normalizedId,
                                cancellationToken: cancellationToken),
                        cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public Task<ContentImageResponse> GetImageMetadataAsync(
                Guid imageId,
                CancellationToken cancellationToken = default)
        {
                ContentServiceEndpointUriResolver.ValidateGuid(id: imageId, paramName: nameof(imageId));
                return this.GetImageMetadataAsync(
                        imageIdentifier: imageId.ToString("D"),
                        cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public async Task<ContentImageResponse> GetImageMetadataAsync(
                string imageIdentifier,
                CancellationToken cancellationToken = default)
        {
                string normalizedId = ContentServiceEndpointUriResolver.NormalizeIdentifier(identifier: imageIdentifier, paramName: nameof(imageIdentifier));
                string apiVersion = ContentServiceEndpointUriResolver.ResolveApiVersion(options: this._options);

                try
                {
                        return await this._endpoints.Value.GetImageMetadataAsync(
                                apiVersion: apiVersion,
                                imageIdentifier: normalizedId,
                                cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                catch (ApiException exception)
                {
                        throw RefitExceptionMapper.ToContentServiceApiException(apiException: exception);
                }
        }

        /// <inheritdoc />
        public Task DeleteImageAsync(
                Guid imageId,
                string? actorId = null,
                CancellationToken cancellationToken = default)
        {
                ContentServiceEndpointUriResolver.ValidateGuid(id: imageId, paramName: nameof(imageId));
                return this.DeleteImageAsync(
                        imageIdentifier: imageId.ToString("D"),
                        actorId: actorId,
                        cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public async Task DeleteImageAsync(
                string imageIdentifier,
                string? actorId = null,
                CancellationToken cancellationToken = default)
        {
                string normalizedId = ContentServiceEndpointUriResolver.NormalizeIdentifier(identifier: imageIdentifier, paramName: nameof(imageIdentifier));
                string? validActorId = ActorHeaderResolver.ResolveValidActorId(
                        explicitActorId: actorId,
                        defaultActorId: this._options.DefaultActorId);
                string apiVersion = ContentServiceEndpointUriResolver.ResolveApiVersion(options: this._options);

                try
                {
                        await this._endpoints.Value.DeleteImageAsync(
                                apiVersion: apiVersion,
                                imageIdentifier: normalizedId,
                                actorId: validActorId,
                                cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                catch (ApiException exception)
                {
                        throw RefitExceptionMapper.ToContentServiceApiException(apiException: exception);
                }
        }

        /// <inheritdoc />
        public Uri GetImageContentUri(Guid imageId)
        {
                ContentServiceEndpointUriResolver.ValidateGuid(id: imageId, paramName: nameof(imageId));
                return this.GetImageContentUri(imageIdentifier: imageId.ToString("D"));
        }

        /// <inheritdoc />
        public Uri GetImageContentUri(string imageIdentifier) =>
                ContentServiceEndpointUriResolver.BuildImageContentUri(
                        httpClient: this._httpClient,
                        options: this._options,
                        imageIdentifier: imageIdentifier);

        /// <inheritdoc />
        public Uri GetImagePreviewUri(Guid imageId)
        {
                ContentServiceEndpointUriResolver.ValidateGuid(id: imageId, paramName: nameof(imageId));
                return this.GetImagePreviewUri(imageIdentifier: imageId.ToString("D"));
        }

        /// <inheritdoc />
        public Uri GetImagePreviewUri(string imageIdentifier) =>
                ContentServiceEndpointUriResolver.BuildImagePreviewUri(
                        httpClient: this._httpClient,
                        options: this._options,
                        imageIdentifier: imageIdentifier);

        /// <summary>
        /// Executes a streaming Refit <see cref="HttpResponseMessage"/> call, validates success via
        /// <see cref="HttpResponseErrorMapper"/> (since Refit skips ExceptionFactory for raw HttpResponseMessage),
        /// and transfers stream/response disposal ownership to <see cref="ContentImageDownloadResponse"/>.
        /// </summary>
        /// <param name="sendAsync">Delegate invoking the Refit streaming endpoint.</param>
        /// <param name="cancellationToken">Token to cancel reading headers or opening the response stream.</param>
        /// <returns>A disposable download wrapper owning both the stream and underlying <see cref="HttpResponseMessage"/>.</returns>
        private static async Task<ContentImageDownloadResponse> DownloadStreamCoreAsync(
                Func<Task<HttpResponseMessage>> sendAsync,
                CancellationToken cancellationToken)
        {
                HttpResponseMessage response = await sendAsync().ConfigureAwait(false);
                Stream? contentStream = null;
                try
                {
                        // Refit bypasses ExceptionFactory when an endpoint returns raw HttpResponseMessage, so we validate manually.
                        await HttpResponseErrorMapper.EnsureSuccessOrThrowAsync(
                                response: response,
                                cancellationToken: cancellationToken).ConfigureAwait(false);

                        contentStream = await response.Content.ReadAsStreamAsync(
                                cancellationToken: cancellationToken).ConfigureAwait(false);

                        // Transfer disposal ownership to ContentImageDownloadResponse; only dispose here if an error occurs before return.
                        return ContentImageDownloadResponse.FromHttpResponse(
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
}
