using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace HighscoreService.Tests.TestDoubles
{
        /// <summary>
        /// Minimal <see cref="ICorsService"/> test double that performs no response mutation.
        /// </summary>
        internal sealed class StubCorsService : ICorsService
        {
                /// <inheritdoc />
                public void ApplyResult(CorsResult result, HttpResponse response)
                {
                        // Intentionally no-op for exception handler unit tests.
                }

                /// <inheritdoc />
                public CorsResult EvaluatePolicy(HttpContext context, CorsPolicy policy)
                {
                        // Return an empty CORS evaluation result so middleware can proceed.
                        return new CorsResult();
                }
        }
}

