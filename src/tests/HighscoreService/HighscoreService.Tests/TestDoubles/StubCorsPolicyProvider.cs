using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace HighscoreService.Tests.TestDoubles
{
        /// <summary>
        /// Minimal <see cref="ICorsPolicyProvider"/> test double returning no configured CORS policy.
        /// </summary>
        internal sealed class StubCorsPolicyProvider : ICorsPolicyProvider
        {
                /// <inheritdoc />
                public Task<CorsPolicy?> GetPolicyAsync(HttpContext context, string? policyName)
                {
                        // Return null so GlobalExceptionHandler treats CORS as unconfigured.
                        return Task.FromResult<CorsPolicy?>(null);
                }
        }
}

