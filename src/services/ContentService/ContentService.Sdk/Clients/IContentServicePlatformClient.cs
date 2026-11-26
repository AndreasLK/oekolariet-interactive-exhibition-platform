using ContentService.Sdk.Models;

namespace ContentService.Sdk.Clients;

/// <summary>
/// Typed HTTP client contract for ContentService publishing bundle resolution, storage health telemetry,
/// and multi-node upload-offer selection.
/// </summary>
public interface IContentServicePlatformClient
{
        /// <summary>
        /// Validates all picture references in an exhibition implementation (and optional template)
        /// and returns a deterministic SHA-256 checksum-sealed offline publish bundle.
        /// </summary>
        /// <param name="query">The implementation JSON and optional template identifier to bundle.</param>
        /// <param name="cancellationToken">A token to cancel the HTTP request.</param>
        /// <returns>The resolved and checksum-verified <see cref="ContentPublishBundleResponse"/>.</returns>
        public Task<ContentPublishBundleResponse> ResolvePublishBundleAsync(
                ContentResolvePublishBundleQuery query,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves current storage quota usage and capacity health status from ContentService.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the HTTP request.</param>
        /// <returns>The <see cref="ContentStorageHealthResponse"/> telemetry snapshot.</returns>
        public Task<ContentStorageHealthResponse> GetStorageHealthAsync(
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Broadcasts an upload-capacity command across the storage pool and returns the first healthy node offer.
        /// </summary>
        /// <param name="command">The upload-offer command specifying the required byte capacity.</param>
        /// <param name="cancellationToken">A token to cancel the HTTP request.</param>
        /// <returns>A <see cref="ContentPoolNodeOfferResponse"/> identifying a healthy storage node with sufficient capacity.</returns>
        public Task<ContentPoolNodeOfferResponse> RequestUploadOfferAsync(
                ContentRequestUploadOfferRequest command,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Broadcasts an upload-capacity request across the storage pool and returns the first healthy node offer.
        /// </summary>
        /// <param name="requiredBytes">The minimum available storage quota in bytes required for the pending upload.</param>
        /// <param name="cancellationToken">A token to cancel the HTTP request.</param>
        /// <returns>A <see cref="ContentPoolNodeOfferResponse"/> identifying a healthy storage node with sufficient capacity.</returns>
        public Task<ContentPoolNodeOfferResponse> RequestUploadOfferAsync(
                long requiredBytes,
                CancellationToken cancellationToken = default);
}
