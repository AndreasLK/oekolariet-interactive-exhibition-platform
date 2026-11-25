using System.Net;
using ContentService.Api.Common;
using ContentService.Domain.Exceptions;
using ContentService.Domain.ValueObjects;

namespace ContentService.Api.Endpoints.Images;

/// <summary>
/// Parses bare GUIDs and shard-aware <c>{node}_{guid}</c> route identifiers while enforcing single-hop forwarding guards.
/// </summary>
internal static class ImageRouteTargetParser
{
        /// <summary>
        /// Parses either a bare GUID or a shard-aware <c>{node}_{guid}</c> external identifier and enforces the single-hop forwarding guard.
        /// </summary>
        /// <param name="imageIdentifier">The raw route identifier.</param>
        /// <param name="httpContext">The active HTTP context.</param>
        /// <param name="localStorageNodeId">The configured local storage node identifier.</param>
        /// <returns>The parsed optional target node ID, image GUID, and whether peer fallback is permitted.</returns>
        /// <exception cref="DomainValidationException">Thrown when <paramref name="imageIdentifier"/> is malformed.</exception>
        /// <exception cref="HttpRequestException">Thrown with HTTP 508 Loop Detected when an already-forwarded request targets another remote node.</exception>
        public static (string? StorageNodeId, Guid ImageId, bool AllowPeerFallback) Parse(
                string imageIdentifier,
                HttpContext httpContext,
                string localStorageNodeId)
        {
                bool isAlreadyForwarded = httpContext.Request.Headers.ContainsKey(ApiConstants.NODE_FORWARDED_HEADER_NAME);

                if (Guid.TryParse(input: imageIdentifier, result: out Guid bareGuid) && bareGuid != Guid.Empty)
                {
                        return (null, bareGuid, !isAlreadyForwarded);
                }

                if (!PictureReference.TryParseExternalId(
                        externalId: imageIdentifier,
                        storageNodeId: out string storageNodeId,
                        imageId: out Guid imageId))
                {
                        throw new DomainValidationException($"Invalid image identifier '{imageIdentifier}'.");
                }

                // WHY: Reject a second cross-node hop when X-Node-Forwarded is already present so misconfigured node routing fails fast instead of looping.
                bool isRemoteNode = !string.Equals(
                        a: storageNodeId,
                        b: localStorageNodeId,
                        comparisonType: StringComparison.OrdinalIgnoreCase);

                if (isAlreadyForwarded && isRemoteNode)
                {
                        throw new HttpRequestException(
                                message: $"Forwarding loop detected for image '{imageIdentifier}' on node '{localStorageNodeId}'.",
                                inner: null,
                                statusCode: HttpStatusCode.LoopDetected);
                }

                return (storageNodeId, imageId, !isAlreadyForwarded);
        }
}

