using HighscoreService.Hub.Api.Options;
using Microsoft.AspNetCore.HttpOverrides;

namespace HighscoreService.Hub.Api.Extensions
{
        /// <summary>
        /// Configures reverse-proxy header forwarding, optional path-base mounting, and CORS for the Hub API.
        /// </summary>
        public static class ReverseProxySetup
        {
                /// <summary>
                /// Named CORS policy applied when <see cref="HubCorsOptions.AllowedOrigins"/> is configured.
                /// </summary>
                public const string CORS_POLICY_NAME = "HubCorsPolicy";

                private const string PATH_BASE_KEY = "PathBase";

                /// <summary>
                /// Registers <see cref="ForwardedHeadersOptions"/> and CORS services for dual internal/external access.
                /// </summary>
                /// <param name="services">The service collection.</param>
                /// <param name="configuration">Application configuration.</param>
                /// <returns>The updated service collection.</returns>
                public static IServiceCollection AddHubReverseProxyAndCors(
                        this IServiceCollection services,
                        IConfiguration configuration)
                {
                        services.Configure<ForwardedHeadersOptions>(options =>
                        {
                                // Forward client IP, original TLS scheme (http vs https), original Host header,
                                // and any path prefix stripped by the reverse proxy (X-Forwarded-Prefix).
                                options.ForwardedHeaders =
                                        ForwardedHeaders.XForwardedFor
                                        | ForwardedHeaders.XForwardedProto
                                        | ForwardedHeaders.XForwardedHost
                                        | ForwardedHeaders.XForwardedPrefix;

                                // Allow multi-hop proxies (e.g., Cloudflare -> internal Nginx/Traefik -> Hub)
                                // without silently truncating the forwarded header chain at 1 hop.
                                options.ForwardLimit = null;

                                // NON-STANDARD DECISION: Trust forwarded headers from any upstream proxy (0.0.0.0/0 & ::/0).
                                // Standard ASP.NET Core only trusts loopback (127.0.0.1 / ::1) and requires a static
                                // KnownProxies / KnownIPNetworks CIDR list in appsettings.json.
                                //
                                // Pros:
                                // - Zero-config resilience when the reverse proxy runs in Docker, on another LAN host,
                                //   or across shifting exhibition VLANs/subnets without brittle CIDR maintenance.
                                // - Works identically whether the venue has internet access or runs offline on local LAN.
                                // Cons:
                                // - Any client able to bypass the reverse proxy and hit Kestrel directly could spoof
                                //   X-Forwarded-For / X-Forwarded-Proto / X-Forwarded-Host / X-Forwarded-Prefix.
                                // Why we picked the non-standard way:
                                // - The Hub sits behind Økolariet's reverse proxy and LAN firewall; maintaining static
                                //   proxy IPs causes operational fragility for minimal gain in this environment.
                                //   Ensure Kestrel's raw port is never exposed directly to untrusted public networks.
                                options.KnownProxies.Clear();
                                options.KnownIPNetworks.Clear();
                        });

                        HubCorsOptions corsOptions = configuration
                                .GetSection(HubCorsOptions.SECTION_NAME)
                                .Get<HubCorsOptions>() ?? new HubCorsOptions();

                        string[] normalizedOrigins =
                        [
                                .. corsOptions.AllowedOrigins
                                        .Where(origin => !string.IsNullOrWhiteSpace(origin))
                                        .Select(origin => origin.Trim().TrimEnd('/'))
                                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        ];

                        // Always register CORS services so GlobalExceptionHandler can resolve ICorsService
                        // and preserve CORS headers on ProblemDetails error responses.
                        services.AddCors(options =>
                        {
                                if (normalizedOrigins.Length == 0)
                                {
                                        return;
                                }

                                options.AddPolicy(CORS_POLICY_NAME, policy =>
                                {
                                        policy.WithOrigins(normalizedOrigins)
                                                .WithMethods("GET", "POST", "DELETE", "OPTIONS")
                                                .AllowAnyHeader()
                                                // Expose Location so browser/Blazor WASM callers can read 201 Created URIs.
                                                .WithExposedHeaders("Location");

                                        // Never combine AllowCredentials() with a wildcard origin ("*"); ASP.NET Core throws at startup.
                                        bool hasWildcard = normalizedOrigins.Contains("*", StringComparer.Ordinal);
                                        if (corsOptions.AllowCredentials && !hasWildcard)
                                        {
                                                policy.AllowCredentials();
                                        }
                                });
                        });

                        return services;
                }

                /// <summary>
                /// Applies forwarded headers, optional path-base prefixing, exception handling, and CORS
                /// in the exact order required by ASP.NET Core behind a reverse proxy.
                /// </summary>
                /// <param name="app">The web application.</param>
                /// <param name="configuration">Application configuration.</param>
                /// <returns>The web application.</returns>
                public static WebApplication UseHubReverseProxyAndCors(
                        this WebApplication app,
                        IConfiguration configuration)
                {
                        // 1. Must run FIRST so Scheme, Host, PathBase (X-Forwarded-Prefix), and RemoteIpAddress
                        //    are rewritten before routing, exception handling, or endpoint URL generation.
                        app.UseForwardedHeaders();

                        // 2. Optional static PathBase for proxies that forward the prefix (e.g., /highscore/api/v1/...)
                        //    instead of stripping it. Direct LAN callers hitting /api/v1/... are unaffected because
                        //    UsePathBase only activates when the incoming path starts with the configured prefix.
                        string? normalizedPathBase = NormalizePathBase(configuration[PATH_BASE_KEY]);
                        if (normalizedPathBase is not null)
                        {
                                app.UsePathBase(normalizedPathBase);
                        }

                        // 3. Exception handler runs after ForwardedHeaders/PathBase so ProblemDetails.Instance
                        //    records the normalized request path.
                        app.UseExceptionHandler();

                        // 4. Apply CORS when explicit origins are configured. Note: GlobalExceptionHandler also
                        //    re-applies CORS headers because ExceptionHandlerMiddleware clears response headers.
                        HubCorsOptions corsOptions = configuration
                                .GetSection(HubCorsOptions.SECTION_NAME)
                                .Get<HubCorsOptions>() ?? new HubCorsOptions();

                        if (corsOptions.AllowedOrigins.Any(origin => !string.IsNullOrWhiteSpace(origin)))
                        {
                                app.UseCors(CORS_POLICY_NAME);
                        }

                        return app;
                }

                private static string? NormalizePathBase(string? rawPathBase)
                {
                        if (string.IsNullOrWhiteSpace(rawPathBase))
                        {
                                return null;
                        }

                        string trimmed = rawPathBase.Trim().TrimEnd('/');
                        if (trimmed.Length == 0)
                        {
                                return null;
                        }

                        // UsePathBase throws ArgumentException if the prefix does not begin with '/'.
                        return trimmed.StartsWith('/') ? trimmed : $"/{trimmed}";
                }
        }
}

