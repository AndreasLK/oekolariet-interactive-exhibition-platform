using System.Net;
using System.Net.Http.Json;
using ContentService.Application.Models;

namespace ContentService.Infrastructure.Nodes;

/// <summary>
/// Executes read-only peer storage node queries (metadata, batch metadata, image streams, and template reference checks) via Dapr.
/// </summary>
internal sealed class DaprStorageNodeQueries
{
        /// <summary>
        /// Shared Dapr sidecar HTTP request sender.
        /// </summary>
        private readonly DaprNodeRequestSender _requestSender;

        /// <summary>
        /// Initializes a new instance of the <see cref="DaprStorageNodeQueries"/> class.
        /// </summary>
        /// <param name="requestSender">The Dapr sidecar HTTP request sender.</param>
        public DaprStorageNodeQueries(DaprNodeRequestSender requestSender)
        {
                ArgumentNullException.ThrowIfNull(requestSender);
                this._requestSender = requestSender;
        }

        /// <summary>
        /// Fetches metadata for a single image from a peer storage node, returning <see langword="null"/> on 404.
        /// </summary>
        /// <param name="storageNodeId">The owning storage node identifier.</param>
        /// <param name="imageId">The unique image identifier.</param>
        /// <param name="cancellationToken">Propagates cancellation.</param>
        /// <returns>The image metadata projection, or <see langword="null"/> if not found.</returns>
        public async Task<ImageMetadataResult?> GetImageMetadataAsync(
                string storageNodeId,
                Guid imageId,
                CancellationToken cancellationToken)
        {
                Uri uri = this._requestSender.BuildDaprInvokeUri(
                        storageNodeId: storageNodeId,
                        relativeApiPath: $"api/v1/images/{storageNodeId}_{imageId}");

                using HttpRequestMessage request = this._requestSender.CreateRequest(method: HttpMethod.Get, uri: uri);
                using HttpResponseMessage response = await this._requestSender.SendAsync(
                        request: request,
                        completionOption: HttpCompletionOption.ResponseContentRead,
                        cancellationToken: cancellationToken);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                        return null;
                }

                await DaprNodeFailureMapper.EnsureSuccessOrThrowDomainAsync(
                        response: response,
                        cancellationToken: cancellationToken);
                return await response.Content.ReadFromJsonAsync<ImageMetadataResult>(
                        options: DaprNodeRequestSender.JSON_OPTIONS,
                        cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Batch-fetches metadata for multiple images hosted on a single peer storage node.
        /// </summary>
        /// <param name="storageNodeId">The owning storage node identifier.</param>
        /// <param name="imageIds">The image identifiers to query.</param>
        /// <param name="cancellationToken">Propagates cancellation.</param>
        /// <returns>The active image metadata projections returned by the peer node.</returns>
        public async Task<IReadOnlyList<ImageMetadataResult>> GetImagesMetadataBatchAsync(
                string storageNodeId,
                IReadOnlyCollection<Guid> imageIds,
                CancellationToken cancellationToken)
        {
                if (imageIds.Count == 0)
                {
                        return [];
                }

                Uri uri = this._requestSender.BuildDaprInvokeUri(
                        storageNodeId: storageNodeId,
                        relativeApiPath: "api/v1/images/batch-metadata");

                using HttpRequestMessage request = this._requestSender.CreateRequest(method: HttpMethod.Post, uri: uri);
                request.Content = JsonContent.Create(inputValue: imageIds, options: DaprNodeRequestSender.JSON_OPTIONS);

                using HttpResponseMessage response = await this._requestSender.SendAsync(
                        request: request,
                        completionOption: HttpCompletionOption.ResponseContentRead,
                        cancellationToken: cancellationToken);

                await DaprNodeFailureMapper.EnsureSuccessOrThrowDomainAsync(
                        response: response,
                        cancellationToken: cancellationToken);
                IReadOnlyList<ImageMetadataResult>? results = await response.Content.ReadFromJsonAsync<IReadOnlyList<ImageMetadataResult>>(
                        options: DaprNodeRequestSender.JSON_OPTIONS,
                        cancellationToken: cancellationToken);

                return results ?? [];
        }

        /// <summary>
        /// Opens a streaming image or preview response from a peer storage node.
        /// </summary>
        /// <param name="storageNodeId">The owning storage node identifier.</param>
        /// <param name="imageId">The unique image identifier.</param>
        /// <param name="isPreview"><see langword="true"/> to request the preview stream; <see langword="false"/> for original content.</param>
        /// <param name="cancellationToken">Propagates cancellation.</param>
        /// <returns>The open stream result, or <see langword="null"/> if not found.</returns>
        public async Task<ImageStreamResult?> OpenImageStreamAsync(
                string storageNodeId,
                Guid imageId,
                bool isPreview,
                CancellationToken cancellationToken)
        {
                string suffix = isPreview ? "preview" : "content";
                Uri uri = this._requestSender.BuildDaprInvokeUri(
                        storageNodeId: storageNodeId,
                        relativeApiPath: $"api/v1/images/{storageNodeId}_{imageId}/{suffix}");

                // WHY: Fetch metadata first so we have FileName, Sha256Hash, and ConcurrencyStamp for ImageStreamResult before opening the raw network stream.
                ImageMetadataResult? metadata = await this.GetImageMetadataAsync(
                        storageNodeId: storageNodeId,
                        imageId: imageId,
                        cancellationToken: cancellationToken);
                if (metadata is null)
                {
                        return null;
                }

                // WHY: Use ResponseHeadersRead so 10 MiB payloads stream straight through without buffering a second copy in memory.
                using HttpRequestMessage request = this._requestSender.CreateRequest(method: HttpMethod.Get, uri: uri);
                HttpResponseMessage response = await this._requestSender.SendAsync(
                        request: request,
                        completionOption: HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken: cancellationToken);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                        response.Dispose();
                        return null;
                }

                try
                {
                        await DaprNodeFailureMapper.EnsureSuccessOrThrowDomainAsync(
                                response: response,
                                cancellationToken: cancellationToken);

                        string effectiveContentType = response.Content.Headers.ContentType?.MediaType ?? metadata.ContentType;
                        long effectiveLength = response.Content.Headers.ContentLength ?? metadata.SizeBytes;
                        ImageMetadataResult effectiveMetadata = isPreview
                                ? metadata with { ContentType = effectiveContentType, SizeBytes = effectiveLength }
                                : metadata;

                        Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken: cancellationToken);
                        HttpResponseOwningStream owningStream = new(innerStream: responseStream, response: response);
                        return new ImageStreamResult(metadata: effectiveMetadata, content: owningStream);
                }
                catch
                {
                        // WHY: Dispose the HttpResponseMessage on failure before HttpResponseOwningStream takes ownership so sockets never leak.
                        response.Dispose();
                        throw;
                }
        }

        /// <summary>
        /// Checks whether any active template on a peer storage node references the specified image.
        /// </summary>
        /// <param name="peerStorageNodeId">The peer storage node to query.</param>
        /// <param name="imageId">The image identifier to check.</param>
        /// <param name="cancellationToken">Propagates cancellation.</param>
        /// <returns><see langword="true"/> if referenced by at least one template on the peer; otherwise <see langword="false"/>.</returns>
        public async Task<bool> IsImageReferencedByAnyTemplateAsync(
                string peerStorageNodeId,
                Guid imageId,
                CancellationToken cancellationToken)
        {
                Uri uri = this._requestSender.BuildDaprInvokeUri(
                        storageNodeId: peerStorageNodeId,
                        relativeApiPath: $"api/v1/templates/image-references/{imageId}");

                using HttpRequestMessage request = this._requestSender.CreateRequest(method: HttpMethod.Get, uri: uri);
                using HttpResponseMessage response = await this._requestSender.SendAsync(
                        request: request,
                        completionOption: HttpCompletionOption.ResponseContentRead,
                        cancellationToken: cancellationToken);

                await DaprNodeFailureMapper.EnsureSuccessOrThrowDomainAsync(
                        response: response,
                        cancellationToken: cancellationToken);
                ReferenceCheckPayload? payload = await response.Content.ReadFromJsonAsync<ReferenceCheckPayload>(
                        options: DaprNodeRequestSender.JSON_OPTIONS,
                        cancellationToken: cancellationToken);

                return payload?.IsReferenced ?? false;
        }
}

