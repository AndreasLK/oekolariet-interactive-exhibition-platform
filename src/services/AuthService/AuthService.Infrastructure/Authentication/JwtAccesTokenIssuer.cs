
using System.Security.Claims;
using AuthService.Application.Authentication;
using AuthService.Domain.Authorization;
using AuthService.Domain.Entities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.Infrastructure.Authentication;

public sealed class JwtAccessTokenIssuer : IAccessTokenIssuer
{
        private const string USERNAME_CLAIM_TYPE = "name";
        private const string ROLE_CLAIM_TYPE = "role";
        private const string PERMISSION_CLAIM_TYPE = "permission";

        private const string INACTIVE_ACCOUNT =
                "Cannot issue a token for an inactive account.";

        private const string INVALID_CONFIGURATION =
                "JWT issuer, audience, or token lifetime is invalid.";

        private const string INVALID_SIGNING_KEY =
                "JWT signing key must contain at least 32 bytes.";

        private const int MIN_SIGNING_KEY_BYTES = 32;

        private readonly string _issuer;
        private readonly string _audience;
        private readonly SymmetricSecurityKey _signingKey;
        private readonly TimeSpan _accessTokenLifetime;
        private readonly TimeProvider _timeProvider;
        private readonly JsonWebTokenHandler _tokenHandler = new();

        public JwtAccessTokenIssuer(
                string issuer,
                string audience,
                byte[] signingKeyBytes,
                TimeSpan accessTokenLifetime,
                TimeProvider timeProvider)
        {
                ArgumentNullException.ThrowIfNull(
                        argument: signingKeyBytes);

                ArgumentNullException.ThrowIfNull(
                        argument: timeProvider);

                if (string.IsNullOrWhiteSpace(issuer)
                        || string.IsNullOrWhiteSpace(audience)
                        || accessTokenLifetime <= TimeSpan.Zero)
                {
                        throw new ArgumentException(
                                message: INVALID_CONFIGURATION);
                }

                if (signingKeyBytes.Length < MIN_SIGNING_KEY_BYTES)
                {
                        throw new ArgumentException(
                                message: INVALID_SIGNING_KEY,
                                paramName: nameof(signingKeyBytes));
                }

                this._issuer = issuer;
                this._audience = audience;
                this._signingKey = new SymmetricSecurityKey(
                        key: signingKeyBytes.ToArray());

                this._accessTokenLifetime = accessTokenLifetime;
                this._timeProvider = timeProvider;
        }

        public IssuedAccessToken Issue(UserAccount account)
        {
                ArgumentNullException.ThrowIfNull(argument: account);

                if (!account.IsActive)
                {
                        throw new InvalidOperationException(
                                message: INACTIVE_ACCOUNT);
                }

                DateTimeOffset issuedAt = this._timeProvider.GetUtcNow();
                DateTimeOffset expiresAt =
                        issuedAt.Add(this._accessTokenLifetime);

                List<Claim> claims =
                [
                        new Claim(
                                type: JwtRegisteredClaimNames.Sub,
                                value: account.Id.Value.ToString()),

                        new Claim(
                                type: JwtRegisteredClaimNames.Jti,
                                value: Guid.NewGuid().ToString()),

                        new Claim(
                                type: USERNAME_CLAIM_TYPE,
                                value: account.Username.Value),

                        new Claim(
                                type: ROLE_CLAIM_TYPE,
                                value: account.Role.ToString())
                ];

                // Include effective permissions, including individual overrides.
                foreach (Permission permission in Enum.GetValues<Permission>())
                {
                        if (!account.HasPermission(permission: permission))
                        {
                                continue;
                        }

                        claims.Add(
                                item: new Claim(
                                        type: PERMISSION_CLAIM_TYPE,
                                        value: permission.ToString()));
                }

                // A signed JWT protects integrity, not confidentiality.
                SecurityTokenDescriptor descriptor = new()
                {
                        Subject = new ClaimsIdentity(claims: claims),
                        Issuer = this._issuer,
                        Audience = this._audience,
                        IssuedAt = issuedAt.UtcDateTime,
                        NotBefore = issuedAt.UtcDateTime,
                        Expires = expiresAt.UtcDateTime,
                        SigningCredentials = new SigningCredentials(
                                key: this._signingKey,
                                algorithm: SecurityAlgorithms.HmacSha256)
                };

                string token = this._tokenHandler.CreateToken(
                        tokenDescriptor: descriptor);

                return new IssuedAccessToken(
                        Value: token,
                        ExpiresAtUtc: expiresAt);
        }
}
