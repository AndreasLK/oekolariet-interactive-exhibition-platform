using ContentService.Sdk.Models;

namespace ContentService.Sdk.Clients;

/// <summary>
/// Typed HTTP client contract for resolving and checksum-verifying offline kiosk publish bundles in ContentService.
/// </summary>
public interface IContentServicePublishingClient
{
        /// <summary>
        /// Resolves all image references and optional template schema for an exhibition implementation into a checksum-verified bundle.
        /// </summary>
        /// <param name="request">The publish bundle resolution request containing implementation JSON and optional template ID.</param>
        /// <param name="cancellationToken">A token to cancel the HTTP request.</param>
        /// <returns>The resolved <see cref="ContentPublishBundle"/> containing verified assets and a deterministic bundle checksum.</returns>
        public Task<ContentPublishBundle> ResolvePublishBundleAsync(
                ResolvePublishBundleRequest request,
                CancellationToken cancellationToken = default);
}

