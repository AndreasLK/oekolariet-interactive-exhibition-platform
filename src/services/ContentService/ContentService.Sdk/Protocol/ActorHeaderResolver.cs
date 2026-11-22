using ContentService.Sdk.Common;

namespace ContentService.Sdk.Protocol;

/// <summary>
/// Resolves and validates optional <c>X-Actor-Id</c> HTTP header values for ContentService SDK requests.
/// </summary>
internal static class ActorHeaderResolver
{
        /// <summary>
        /// Resolves the effective <c>X-Actor-Id</c> header value, omitting null/whitespace values and rejecting non-ASCII/control characters.
        /// </summary>
        /// <param name="explicitActorId">The per-request actor identifier, if provided.</param>
        /// <param name="defaultActorId">The host's configured fallback actor identifier.</param>
        /// <returns>The validated ASCII actor identifier, or <see langword="null"/> when omitted.</returns>
        public static string? ResolveValidActorId(
                string? explicitActorId,
                string? defaultActorId)
        {
                // Priority: per-call ActorId overrides the host's configured DefaultActorId; if both are empty, omit the header.
                string? candidate = explicitActorId ?? defaultActorId;

                // Omit blank values so ContentService.Api's RequestActorResolver falls back cleanly to its configured system actor instead of rejecting whitespace.
                if (string.IsNullOrWhiteSpace(candidate))
                {
                        return null;
                }

                // HTTP headers only permit printable 7-bit ASCII (code points 32..126); fail with a clear ArgumentException before HttpHeaders rejects it.
                if (!SdkConstants.IsPrintableAsciiHeaderValue(value: candidate))
                {
                        throw new ArgumentException(
                                message: "ActorId must contain only printable ASCII characters.",
                                paramName: nameof(explicitActorId));
                }

                return candidate;
        }
}

