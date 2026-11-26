using ContentService.Sdk.Models;
using Refit;

namespace ContentService.Sdk.Protocol;

/// <summary>
/// Internal Refit HTTP contract for <c>ContentService.Api</c> publishing, storage health, and pool endpoints.
/// </summary>
/// <remarks>
/// Kept <see langword="internal"/> behind <c>ContentServicePlatformClient</c> so Refit exceptions and versioning
/// details never leak into consumer code (see <see cref="IContentServiceImageEndpoints"/> for the trade-offs).
/// </remarks>
internal interface IContentServicePlatformEndpoints
{
        /// <summary>
        /// Resolves and checksum-seals an offline kiosk publish bundle.
        /// </summary>
        /// <param name="apiVersion">API version route segment.</param>
        /// <param name="query">Bundle resolution request specifying template IDs.</param>
        /// <param name="cancellationToken">Token to cancel the HTTP request.</param>
        /// <returns>The resolved publish bundle with asset checksums.</returns>
        [Post("/api/{apiVersion}/publish-bundles/resolve")]
        public Task<ContentPublishBundleResponse> ResolvePublishBundleAsync(
                string apiVersion,
                [Body] ContentResolvePublishBundleQuery query,
                CancellationToken cancellationToken);

        /// <summary>
        /// Retrieves storage quota usage and capacity health telemetry.
        /// </summary>
        /// <param name="apiVersion">API version route segment.</param>
        /// <param name="cancellationToken">Token to cancel the HTTP request.</param>
        /// <returns>Current node storage health and quota telemetry.</returns>
        [Get("/api/{apiVersion}/health/storage")]
        public Task<ContentStorageHealthResponse> GetStorageHealthAsync(
                string apiVersion,
                CancellationToken cancellationToken);

        /// <summary>
        /// Requests a healthy storage node upload offer across the ContentService pool.
        /// </summary>
        /// <param name="apiVersion">API version route segment.</param>
        /// <param name="command">Upload capacity requirements.</param>
        /// <param name="cancellationToken">Token to cancel the HTTP request.</param>
        /// <returns>The selected storage node offer.</returns>
        [Post("/api/{apiVersion}/pool/upload-offers")]
        public Task<ContentPoolNodeOfferResponse> RequestUploadOfferAsync(
                string apiVersion,
                [Body] ContentRequestUploadOfferCommand command,
                CancellationToken cancellationToken);
}

