namespace ContentService.Api.Common;

/// <summary>
/// Resolves a trustworthy audit actor identifier from the active HTTP request context without allowing authenticated principals to be spoofed.
/// </summary>
internal static class RequestActorResolver
{
        /// <summary>
        /// Resolves a trustworthy audit actor identifier so authenticated principals cannot be spoofed by client headers while invalid explicit inputs still fail validation.
        /// </summary>
        /// <param name="httpContext">The active HTTP context providing authenticated claims and request headers.</param>
        /// <param name="explicitActorId">Optional actor identifier supplied directly in a JSON request body.</param>
        /// <returns>The resolved actor identifier passed to Application commands for invariant validation and audit logging.</returns>
        public static string ResolveActorId(HttpContext httpContext, string? explicitActorId = null)
        {
                ArgumentNullException.ThrowIfNull(httpContext);

                // Prioritize authenticated principal identity over client-supplied headers/fields so callers cannot spoof audit logs once auth is enabled.
                string? identityName = httpContext.User.Identity?.Name;
                if (!string.IsNullOrWhiteSpace(identityName))
                {
                        return identityName;
                }

                // Preserve explicit non-null inputs (including invalid whitespace) so Application/Domain validation rejects malformed actors instead of masking them.
                if (explicitActorId is not null)
                {
                        return explicitActorId;
                }

                if (httpContext.Request.Headers.TryGetValue(
                        key: ApiConstants.ACTOR_HEADER_NAME,
                        value: out Microsoft.Extensions.Primitives.StringValues headerValues))
                {
                        // Forward raw header text (even if blank) so invalid actor headers fail loudly instead of silently falling back to the default actor.
                        return headerValues.ToString();
                }

                return ApiConstants.DEFAULT_ACTOR_ID;
        }
}

