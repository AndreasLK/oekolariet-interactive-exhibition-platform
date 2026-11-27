using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using HighscoreService.Application.Interfaces;
using HighscoreService.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace HighscoreService.Hub.Api.Authentication
{
        /// <summary>
        /// Authenticates exhibition screen servers presenting <c>Bearer scr_&lt;screenId&gt;.&lt;secret&gt;</c> tokens.
        /// </summary>
        /// <remarks>
        /// NON-STANDARD DECISION: Uses opaque per-screen bearer secrets hashed with SHA-256 in SQL Server
        /// instead of custom per-request ECDSA HTTP signatures or static hardcoded API keys.
        /// <para>
        /// Pros: Zero hardcoded secrets in binaries, zero `.env` setup for non-technical installers,
        /// immune to reverse-proxy PathBase stripping and kiosk clock skew, and supports instant revocation.
        /// Cons: Relies on TLS at the reverse proxy for wire confidentiality and performs a primary-key
        /// lookup per sync request (negligible at exhibition poll rates).
        /// </para>
        /// </remarks>
        public sealed class ScreenTokenAuthenticationHandler(
                IOptionsMonitor<AuthenticationSchemeOptions> options,
                ILoggerFactory logger,
                UrlEncoder encoder,
                IScreenRegistrationStore registrationStore)
                : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
        {
                /// <summary>
                /// Authentication scheme name for enrolled exhibition screens.
                /// </summary>
                public const string SCHEME_NAME = "ScreenToken";

                /// <summary>
                /// Token prefix distinguishing screen bearer credentials from AuthService JWTs.
                /// </summary>
                public const string TOKEN_PREFIX = "scr_";

                private readonly IScreenRegistrationStore _registrationStore = registrationStore;

                /// <inheritdoc />
                protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
                {
                        // Ignore requests without a 'Bearer scr_...' token so JWT bearer auth can handle user tokens.
                        if (!AuthenticationHeaderValue.TryParse(this.Request.Headers.Authorization, out AuthenticationHeaderValue? header)
                                || !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
                                || string.IsNullOrWhiteSpace(header.Parameter)
                                || !header.Parameter.StartsWith(TOKEN_PREFIX, StringComparison.Ordinal))
                        {
                                return AuthenticateResult.NoResult();
                        }

                        // Split '<screenId>.<secret>' after the 'scr_' prefix.
                        string payload = header.Parameter[TOKEN_PREFIX.Length..];
                        int separatorIndex = payload.IndexOf('.');
                        if (separatorIndex <= 0 || separatorIndex >= payload.Length - 1)
                        {
                                return AuthenticateResult.Fail("Malformed screen bearer token.");
                        }

                        if (!Guid.TryParse(payload[..separatorIndex], out Guid screenId))
                        {
                                return AuthenticateResult.Fail("Invalid screen identifier in bearer token.");
                        }

                        // Look up the screen registration and require both Approved status and constant-time secret match.
                        string rawSecret = payload[(separatorIndex + 1)..];
                        ScreenRegistration? registration = await this._registrationStore.GetByIdAsync(
                                screenId,
                                this.Context.RequestAborted);

                        if (registration is null || registration.Status != ScreenRegistrationStatus.Approved)
                        {
                                return AuthenticateResult.Fail("Screen is not registered or not approved.");
                        }

                        if (!registration.VerifySecret(rawSecret))
                        {
                                return AuthenticateResult.Fail("Invalid screen credential.");
                        }

                        // Issue an authenticated principal carrying only the Screen role.
                        Claim[] claims =
                        [
                                new Claim(ClaimTypes.NameIdentifier, registration.ScreenId.ToString()),
                                new Claim(ClaimTypes.Name, registration.NodeId),
                                new Claim(ClaimTypes.Role, "Screen")
                        ];

                        ClaimsIdentity identity = new(claims, SCHEME_NAME);
                        AuthenticationTicket ticket = new(new ClaimsPrincipal(identity), SCHEME_NAME);
                        return AuthenticateResult.Success(ticket);
                }
        }
}

