using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContentService.Tests.Api.Fakes;

/// <summary>
/// Minimal authentication handler authenticating every request as <see cref="TestUserIdentityOptions.UserName"/>.
/// </summary>
internal sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
        public const string SCHEME_NAME = "TestScheme";
        private readonly TestUserIdentityOptions _userOptions;

        public TestAuthHandler(
                IOptionsMonitor<AuthenticationSchemeOptions> options,
                ILoggerFactory logger,
                UrlEncoder encoder,
                TestUserIdentityOptions userOptions)
                : base(options: options, logger: logger, encoder: encoder)
        {
                this._userOptions = userOptions;
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
                Claim[] claims = [new Claim(type: ClaimTypes.Name, value: this._userOptions.UserName)];
                ClaimsIdentity identity = new(claims: claims, authenticationType: SCHEME_NAME);
                ClaimsPrincipal principal = new(identity: identity);
                AuthenticationTicket ticket = new(principal: principal, authenticationScheme: SCHEME_NAME);
                return Task.FromResult(result: AuthenticateResult.Success(ticket: ticket));
        }
}

