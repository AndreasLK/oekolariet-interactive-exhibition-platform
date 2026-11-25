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
        public async Task<ContentImageMetadata> UploadImageAsync(
                UploadImageRequest request,
                CancellationToken cancellationToken = default)
        {
                ArgumentNullException.ThrowIfNull(request);

                // Validate actor header before wrapping the stream so invalid headers fail without touching request.Content.
                string? actorId = this.ResolveValidActorId(explicitActorId: request.ActorId);
                Uri requestUri = ContentServiceEndpointUriResolver.BuildImagesRootUri(
                        httpClient: this._httpClient,
                        options: this._options);

                using MultipartFormDataContent multipartContent = ImageUploadMultipartContentFactory.Create(request: request);
                using HttpRequestMessage httpRequest = new(method: HttpMethod.Post, requestUri: requestUri)
                {
                        Content = multipartContent
                };

                if (actorId is not null)
                {
                        httpRequest.Headers.Add(name: SdkConstants.ACTOR_HEADER_NAME, value: actorId);
                }

                using HttpResponseMessage response = await this._httpClient.SendAsync(
                        request: httpRequest,
                        completionOption: HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                await HttpResponseErrorMapper.EnsureSuccessOrThrowAsync(
                        response: response,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                return await ReadMetadataPayloadAsync(
                        response: response,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<ContentImageDownload> DownloadImageAsync(
                Guid imageId,
                CancellationToken cancellationToken = default)
        {
                Uri requestUri = this.GetImageContentUri(imageId: imageId);
                using HttpRequestMessage httpRequest = new(method: HttpMethod.Get, requestUri: requestUri);

                HttpResponseMessage response = await this._httpClient.SendAsync(
                        request: httpRequest,
                        completionOption: HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                // Do not wrap response in a using block on success because ContentImageDownload owns its disposal.
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

                using HttpRequestMessage httpRequest = new(method: HttpMethod.Get, requestUri: requestUri);
                using HttpResponseMessage response = await this._httpClient.SendAsync(
                        request: httpRequest,
                        completionOption: HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                await HttpResponseErrorMapper.EnsureSuccessOrThrowAsync(
                        response: response,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                return await ReadMetadataPayloadAsync(
                        response: response,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public Uri GetImageContentUri(Guid imageId) =>
                ContentServiceEndpointUriResolver.BuildImageContentUri(
                        httpClient: this._httpClient,
                        options: this._options,
                        imageId: imageId);

        /// <summary>
        /// Reads and validates a <see cref="ContentImageMetadata"/> JSON payload from a successful HTTP response.
        /// </summary>
        /// <param name="response">The successful HTTP response.</param>
        /// <param name="cancellationToken">A token to cancel reading.</param>
        /// <returns>The deserialized <see cref="ContentImageMetadata"/> instance.</returns>
        private static async Task<ContentImageMetadata> ReadMetadataPayloadAsync(
                HttpResponseMessage response,
                CancellationToken cancellationToken)
        {
                ContentImageMetadata? metadata = await response.Content.ReadFromJsonAsync<ContentImageMetadata>(
                        options: SdkConstants.JSON_SERIALIZER_OPTIONS,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                return metadata ?? throw new InvalidOperationException("ContentService returned an empty metadata payload.");
        }

        /// <summary>
        /// Resolves the effective <c>X-Actor-Id</c> header value, omitting null/whitespace values and rejecting non-ASCII/control characters.
        /// </summary>
        /// <param name="explicitActorId">The per-request actor identifier, if provided.</param>
        /// <returns>The validated ASCII actor identifier, or <see langword="null"/> when omitted.</returns>
        private string? ResolveValidActorId(string? explicitActorId)
        {
                string? candidate = explicitActorId ?? this._options.DefaultActorId;

                // Omit blank values so ContentService's RequestActorResolver falls back cleanly instead of rejecting whitespace.
                if (string.IsNullOrWhiteSpace(candidate))
                {
                        return null;
                }

                if (!SdkConstants.IsPrintableAsciiHeaderValue(value: candidate))
                {
                        throw new ArgumentException(
                                message: "ActorId must contain only printable ASCII characters.",
                                paramName: nameof(explicitActorId));
                }

                return candidate;
        }
}
