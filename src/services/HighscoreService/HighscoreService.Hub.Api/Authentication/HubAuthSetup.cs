using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

namespace HighscoreService.Hub.Api.Authentication
{
        /// <summary>
        /// Configures dual authentication (<c>AuthService</c> JWTs + <c>ScreenToken</c> bearer credentials),
        /// authorization policies, and enrollment rate limiting for <c>HighscoreService.Hub.Api</c>.
        /// </summary>
        public static class HubAuthSetup
        {
                /// <summary>
                /// Policy scheme routing <c>Bearer scr_*</c> to <see cref="ScreenTokenAuthenticationHandler"/>
                /// and all other bearer tokens to <see cref="JwtBearerDefaults.AuthenticationScheme"/>.
                /// </summary>
                public const string POLICY_SCHEME_NAME = "HubBearerSelector";

                /// <summary>
                /// Authorization policy allowing only approved exhibition screen servers (or Hub admins).
                /// </summary>
                public const string POLICY_SCREEN_ONLY = "ScreenOnly";

                /// <summary>
                /// Authorization policy allowing approved exhibition screen servers or authenticated human users.
                /// </summary>
                public const string POLICY_SCREEN_OR_USER = "ScreenOrUser";

                /// <summary>
                /// Authorization policy allowing only authenticated <c>AuthService</c> human users.
                /// </summary>
                public const string POLICY_AUTHENTICATED_USER = "AuthenticatedUser";

                /// <summary>
                /// Authorization policy allowing only <c>AuthService</c> administrators or privileged editors.
                /// </summary>
                public const string POLICY_HUB_ADMIN = "HubAdmin";

                /// <summary>
                /// Fixed-window rate limiter policy name protecting anonymous screen enrollment.
                /// </summary>
                public const string ENROLL_RATE_LIMIT_POLICY = "ScreenEnrollLimiter";

                private const int JWT_CLOCK_SKEW_SECONDS = 30;
                private const int ENROLL_PERMIT_LIMIT_PER_MINUTE = 10;

                /// <summary>
                /// Registers Hub authentication schemes, authorization policies, and enrollment rate limiting.
                /// </summary>
                /// <param name="services">Service collection.</param>
                /// <param name="configuration">Application configuration.</param>
                /// <returns>The updated service collection.</returns>
                public static IServiceCollection AddHubAuthenticationAndAuthorization(
                        this IServiceCollection services,
                        IConfiguration configuration)
                {
                        string issuer = configuration["Jwt:Issuer"] ?? "Oekolariet.AuthService";
                        string audience = configuration["Jwt:Audience"] ?? "Oekolariet.Platform";
                        string? signingKeyBase64 = configuration["Jwt:SigningKey"];

                        // NON-STANDARD DECISION: AuthService currently issues symmetric HS256 JWTs without a JWKS endpoint,
                        // so Hub.Api validates tokens using the shared Jwt:SigningKey from configuration/secrets.
                        // Pros: Immediate integration with AuthService without modifying AuthService's token issuer.
                        // Cons: Compromising Hub.Api exposes the symmetric signing key; migrate to ES256/RS256 + JWKS later.
                        byte[] signingKeyBytes = string.IsNullOrWhiteSpace(signingKeyBase64)
                                ? []
                                : Convert.FromBase64String(signingKeyBase64);

                        services.AddAuthentication(options =>
                        {
                                // Route every request through HubBearerSelector so a single endpoint can accept
                                // either an AuthService user JWT or an exhibition screen's opaque scr_ token.
                                options.DefaultAuthenticateScheme = POLICY_SCHEME_NAME;
                                options.DefaultChallengeScheme = POLICY_SCHEME_NAME;
                        })
                        .AddPolicyScheme(POLICY_SCHEME_NAME, POLICY_SCHEME_NAME, options =>
                        {
                                options.ForwardDefaultSelector = context =>
                                {
                                        // Inspect the Bearer prefix to pick the right validator without double-challenging:
                                        // 'Bearer scr_<screenId>.<secret>' -> ScreenTokenAuthenticationHandler (SQL Server hash check)
                                        // Any other 'Bearer <jwt>'         -> JwtBearer (AuthService HS256 signature check)
                                        string? authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                                        if (!string.IsNullOrWhiteSpace(authHeader)
                                                && authHeader.StartsWith($"Bearer {ScreenTokenAuthenticationHandler.TOKEN_PREFIX}", StringComparison.OrdinalIgnoreCase))
                                        {
                                                return ScreenTokenAuthenticationHandler.SCHEME_NAME;
                                        }

                                        return JwtBearerDefaults.AuthenticationScheme;
                                };
                        })
                        .AddScheme<AuthenticationSchemeOptions, ScreenTokenAuthenticationHandler>(
                                ScreenTokenAuthenticationHandler.SCHEME_NAME,
                                _ => { })
                        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
                        {
                                // Preserve raw 'role' and 'permission' claim names from AuthService instead of remapping to WS-Federation URIs.
                                options.MapInboundClaims = false;
                                options.TokenValidationParameters = new TokenValidationParameters
                                {
                                        ValidateIssuer = true,
                                        ValidIssuer = issuer,
                                        ValidateAudience = true,
                                        ValidAudience = audience,
                                        ValidateLifetime = true,
                                        ClockSkew = TimeSpan.FromSeconds(JWT_CLOCK_SKEW_SECONDS),
                                        ValidateIssuerSigningKey = true,
                                        IssuerSigningKey = signingKeyBytes.Length >= 32
                                                ? new SymmetricSecurityKey(signingKeyBytes)
                                                : new SymmetricSecurityKey(Encoding.UTF8.GetBytes("UNCONFIGURED_JWT_SIGNING_KEY_PLACEHOLDER_32B")),
                                        RoleClaimType = "role",
                                        NameClaimType = "name"
                                };
                        });

                        // Authorization policies enforce least-privilege access across four distinct caller tiers:
                        // - ScreenOnly:        Approved Screen servers (scr_ token) or Hub admins.
                        // - ScreenOrUser:      Approved Screen servers or logged-in AuthService users (score submission).
                        // - AuthenticatedUser: Human AuthService users only (exhibition analytics/statistics).
                        // - HubAdmin:          Admin role or DeleteGame/ManageAccounts permission (deletes, screen approval).
                        services.AddAuthorizationBuilder()
                                .AddPolicy(POLICY_SCREEN_ONLY, policy =>
                                        policy.RequireAssertion(ctx =>
                                                ctx.User.Identity?.AuthenticationType == ScreenTokenAuthenticationHandler.SCHEME_NAME
                                                || IsHubAdmin(ctx.User)))
                                .AddPolicy(POLICY_SCREEN_OR_USER, policy =>
                                        policy.RequireAuthenticatedUser())
                                .AddPolicy(POLICY_AUTHENTICATED_USER, policy =>
                                        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                                              .RequireAuthenticatedUser())
                                .AddPolicy(POLICY_HUB_ADMIN, policy =>
                                        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                                              .RequireAuthenticatedUser()
                                              .RequireAssertion(ctx => IsHubAdmin(ctx.User)));

                        services.AddRateLimiter(options =>
                        {
                                // Protect the anonymous /api/v1/screens/enroll endpoint from flood/DoS attacks by limiting
                                // each client IP to 10 enrollment attempts per minute (paired with the 50-row pending queue cap).
                                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                                options.AddPolicy(ENROLL_RATE_LIMIT_POLICY, httpContext =>
                                {
                                        string partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                                        return RateLimitPartition.GetFixedWindowLimiter(
                                                partitionKey: partitionKey,
                                                factory: _ => new FixedWindowRateLimiterOptions
                                                {
                                                        PermitLimit = ENROLL_PERMIT_LIMIT_PER_MINUTE,
                                                        Window = TimeSpan.FromMinutes(1),
                                                        QueueLimit = 0
                                                });
                                });
                        });

                        return services;
                }

                private static bool IsHubAdmin(System.Security.Claims.ClaimsPrincipal user)
                {
                        if (user.IsInRole("Admin"))
                        {
                                return true;
                        }

                        return user.Claims.Any(c =>
                                string.Equals(c.Type, "permission", StringComparison.Ordinal)
                                && (string.Equals(c.Value, "DeleteGame", StringComparison.Ordinal)
                                        || string.Equals(c.Value, "ManageAccounts", StringComparison.Ordinal)));
                }
        }
}

