using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace ContentService.Api.Common;

/// <summary>
/// Builds the Blazor CORS policy via <see cref="IConfigureOptions{CorsOptions}"/> so environment and test-host origin overrides are respected after configuration is finalized.
/// </summary>
internal sealed class BlazorCorsOptionsSetup : IConfigureOptions<CorsOptions>
{
        /// <summary>
        /// Host configuration supplying the environment-specific allowed Blazor origin list.
        /// </summary>
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Captures host configuration so CORS origins are read lazily after all config providers (including test overrides) are built.
        /// </summary>
        /// <param name="configuration">The application configuration.</param>
        public BlazorCorsOptionsSetup(IConfiguration configuration)
        {
                ArgumentNullException.ThrowIfNull(configuration);
                this._configuration = configuration;
        }

        /// <inheritdoc />
        public void Configure(CorsOptions options)
        {
                ArgumentNullException.ThrowIfNull(options);

                // Trim whitespace and trailing slashes because browsers send Origin without a trailing slash and exact-match failures break CORS silently.
                string[] allowedCorsOrigins = (this._configuration
                        .GetSection(ApiConstants.CORS_ALLOWED_ORIGINS_SECTION)
                        .Get<string[]>() ?? [])
                        .Where(origin => !string.IsNullOrWhiteSpace(origin))
                        .Select(origin => origin.Trim().TrimEnd('/'))
                        .ToArray();

                options.AddPolicy(
                        name: ApiConstants.BLAZOR_CORS_POLICY_NAME,
                        configurePolicy: policy => ConfigureCorsPolicy(policy: policy, allowedCorsOrigins: allowedCorsOrigins));
        }

        /// <summary>
        /// Configures fail-closed origin matching and exposes cache/resource headers required by browser-hosted Blazor WASM clients.
        /// </summary>
        /// <param name="policy">The CORS policy builder being configured.</param>
        /// <param name="allowedCorsOrigins">Normalized allowed origin URLs.</param>
        private static void ConfigureCorsPolicy(CorsPolicyBuilder policy, string[] allowedCorsOrigins)
        {
                // Leave Origins empty when unconfigured so deployments fail closed instead of accidentally exposing wildcard cross-origin access.
                if (allowedCorsOrigins.Length > 0)
                {
                        policy.WithOrigins(allowedCorsOrigins);
                }

                // Expose Location, Content-Disposition, and Content-Length because browsers hide non-safelisted headers from Blazor WASM HttpClient otherwise.
                policy.AllowAnyHeader()
                        .AllowAnyMethod()
                        .WithExposedHeaders(
                                HeaderNames.Location,
                                HeaderNames.ContentDisposition,
                                HeaderNames.ContentLength);
        }
}

