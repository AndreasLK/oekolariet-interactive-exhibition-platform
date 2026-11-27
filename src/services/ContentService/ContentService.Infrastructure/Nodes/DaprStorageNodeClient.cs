using ContentService.Application.Interfaces;
using ContentService.Application.Models;

namespace ContentService.Infrastructure.Nodes;

/// <summary>
/// Invokes peer <c>ContentService</c> storage nodes through the local Dapr sidecar HTTP service invocation API (<c>/v1.0/invoke/contentservice-{node}/method/...</c>).
/// </summary>
internal sealed class DaprStorageNodeClient : IStorageNodeClient
{
        /// <summary>
        /// Named <see cref="HttpClient"/> key configured with pooling for Dapr sidecar communication.
        /// </summary>
        public const string DAPR_HTTP_CLIENT_NAME = DaprNodeRequestSender.DAPR_HTTP_CLIENT_NAME;

        /// <summary>
        /// Collaborator handling read-only peer storage node queries.
        /// </summary>
        private readonly DaprStorageNodeQueries _queries;

        /// <summary>
        /// Collaborator handling mutating peer storage node commands.
        /// </summary>
        private readonly DaprStorageNodeCommands _commands;

        /// <summary>
        /// Initializes a new instance of the <see cref="DaprStorageNodeClient"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The pooled HTTP client factory.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpClientFactory"/> is <see langword="null"/>.</exception>
        public DaprStorageNodeClient(IHttpClientFactory httpClientFactory)
        {
                ArgumentNullException.ThrowIfNull(httpClientFactory);

                // Share a single request sender so queries and commands reuse the same Dapr HTTP client pool.
                DaprNodeRequestSender requestSender = new(httpClientFactory: httpClientFactory);
                this._queries = new DaprStorageNodeQueries(requestSender: requestSender);
                this._commands = new DaprStorageNodeCommands(requestSender: requestSender);
        }

        /// <inheritdoc />
        public Task<ImageMetadataResult?> GetImageMetadataAsync(
                string storageNodeId,
                Guid imageId,
                CancellationToken cancellationToken)
        {
                return this._queries.GetImageMetadataAsync(
                        storageNodeId: storageNodeId,
                        imageId: imageId,
                        cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<ImageMetadataResult>> GetImagesMetadataBatchAsync(
                string storageNodeId,
                IReadOnlyCollection<Guid> imageIds,
                CancellationToken cancellationToken)
        {
                return this._queries.GetImagesMetadataBatchAsync(
                        storageNodeId: storageNodeId,
                        imageIds: imageIds,
                        cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public Task<ImageStreamResult?> OpenImageStreamAsync(
                string storageNodeId,
                Guid imageId,
                bool isPreview,
                CancellationToken cancellationToken)
        {
                return this._queries.OpenImageStreamAsync(
                        storageNodeId: storageNodeId,
                        imageId: imageId,
                        isPreview: isPreview,
                        cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public Task<ImageMetadataResult> UpdateImageMetadataAsync(
                string storageNodeId,
                UpdateImageMetadataCommand command,
                CancellationToken cancellationToken)
        {
                return this._commands.UpdateImageMetadataAsync(
                        storageNodeId: storageNodeId,
                        command: command,
                        cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public Task DeleteImageAsync(
                string storageNodeId,
                DeleteImageCommand command,
                CancellationToken cancellationToken)
        {
                return this._commands.DeleteImageAsync(
                        storageNodeId: storageNodeId,
                        command: command,
                        cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public Task<bool> IsImageReferencedByAnyTemplateAsync(
                string peerStorageNodeId,
                string imageStorageNodeId,
                Guid imageId,
                CancellationToken cancellationToken)
        {
                return this._queries.IsImageReferencedByAnyTemplateAsync(
                        peerStorageNodeId: peerStorageNodeId,
                        imageId: imageId,
                        cancellationToken: cancellationToken);
        }
}
