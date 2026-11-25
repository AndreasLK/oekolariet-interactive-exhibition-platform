using System.Net.Http.Json;
using ContentService.Application.Models;

namespace ContentService.Infrastructure.Nodes;

/// <summary>
/// Executes mutating peer storage node commands (metadata updates and soft-deletions) via Dapr.
/// </summary>
internal sealed class DaprStorageNodeCommands
{
        /// <summary>
        /// Shared Dapr sidecar HTTP request sender.
        /// </summary>
        private readonly DaprNodeRequestSender _requestSender;

        /// <summary>
        /// Initializes a new instance of the <see cref="DaprStorageNodeCommands"/> class.
        /// </summary>
        /// <param name="requestSender">The Dapr sidecar HTTP request sender.</param>
        public DaprStorageNodeCommands(DaprNodeRequestSender requestSender)
        {
                ArgumentNullException.ThrowIfNull(requestSender);
                this._requestSender = requestSender;
        }

        /// <summary>
        /// Forwards an image metadata patch command to the owning peer storage node.
        /// </summary>
        /// <param name="storageNodeId">The owning storage node identifier.</param>
        /// <param name="command">The metadata update command payload.</param>
        /// <param name="cancellationToken">Propagates cancellation.</param>
        /// <returns>The updated <see cref="ImageMetadataResult"/> returned by the peer.</returns>
        public async Task<ImageMetadataResult> UpdateImageMetadataAsync(
                string storageNodeId,
                UpdateImageMetadataCommand command,
                CancellationToken cancellationToken)
        {
                ArgumentNullException.ThrowIfNull(command);

                // Target the owning node's v1 metadata endpoint via Dapr service invocation.
                Uri uri = this._requestSender.BuildDaprInvokeUri(
                        storageNodeId: storageNodeId,
                        relativeApiPath: $"api/v1/images/{storageNodeId}_{command.ImageId}/metadata");

                // Attach the original actor header so audit logs on the remote node record the caller.
                using HttpRequestMessage request = this._requestSender.CreateRequest(
                        method: HttpMethod.Patch,
                        uri: uri,
                        actorId: command.ActorId);

                request.Content = JsonContent.Create(
                        inputValue: new
                        {
                                command.AltText,
                                command.Tags,
                                command.AlbumPath,
                                command.ExpectedConcurrencyStamp
                        },
                        options: DaprNodeRequestSender.JSON_OPTIONS);

                // Buffer the small JSON response and translate any remote HTTP errors into domain exceptions.
                using HttpResponseMessage response = await this._requestSender.SendAsync(
                        request: request,
                        completionOption: HttpCompletionOption.ResponseContentRead,
                        cancellationToken: cancellationToken);

                await DaprNodeFailureMapper.EnsureSuccessOrThrowDomainAsync(
                        response: response,
                        cancellationToken: cancellationToken);
                ImageMetadataResult? updated = await response.Content.ReadFromJsonAsync<ImageMetadataResult>(
                        options: DaprNodeRequestSender.JSON_OPTIONS,
                        cancellationToken: cancellationToken);

                return updated ?? throw new HttpRequestException(
                        message: $"Remote storage node '{storageNodeId}' returned an empty metadata update response.");
        }

        /// <summary>
        /// Forwards a soft-delete command to the owning peer storage node.
        /// </summary>
        /// <param name="storageNodeId">The owning storage node identifier.</param>
        /// <param name="command">The delete command payload.</param>
        /// <param name="cancellationToken">Propagates cancellation.</param>
        public async Task DeleteImageAsync(
                string storageNodeId,
                DeleteImageCommand command,
                CancellationToken cancellationToken)
        {
                ArgumentNullException.ThrowIfNull(command);

                // Target the owning node's v1 delete endpoint via Dapr service invocation.
                Uri uri = this._requestSender.BuildDaprInvokeUri(
                        storageNodeId: storageNodeId,
                        relativeApiPath: $"api/v1/images/{storageNodeId}_{command.ImageId}");

                using HttpRequestMessage request = this._requestSender.CreateRequest(
                        method: HttpMethod.Delete,
                        uri: uri,
                        actorId: command.ActorId);

                using HttpResponseMessage response = await this._requestSender.SendAsync(
                        request: request,
                        completionOption: HttpCompletionOption.ResponseContentRead,
                        cancellationToken: cancellationToken);

                // Map 409 Conflict from the peer into EntityInUseException when a template still references the image.
                await DaprNodeFailureMapper.EnsureSuccessOrThrowDomainAsync(
                        response: response,
                        cancellationToken: cancellationToken,
                        conflictAsEntityInUse: true);
        }
}
