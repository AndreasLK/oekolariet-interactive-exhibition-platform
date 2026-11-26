
using System.Security.Cryptography;
using System.Text;
using AuthService.Application.Authentication;
using AuthService.Domain.ValueObjects;

namespace AuthService.Infrastructure.Authentication;

public sealed class RefreshTokenService : IRefreshTokenService
{
        private const int TOKEN_BYTE_LENGTH = 32;

        private const string TOKEN_REQUIRED =
                "Refresh token is required.";

        public IssuedRefreshToken Generate()
        {
                // Cryptographically secure randomness provides 256 bits of entropy.
                byte[] randomBytes = RandomNumberGenerator.GetBytes(
                        count: TOKEN_BYTE_LENGTH);

                // Base64Url encoding makes the token safe for HTTP transport.
                string token = Convert.ToBase64String(
                        inArray: randomBytes)
                        .TrimEnd('=')
                        .Replace(oldChar: '+', newChar: '-')
                        .Replace(oldChar: '/', newChar: '_');

                return new IssuedRefreshToken(
                        value: token,
                        hash: this.ComputeHash(token: token));
        }

        public RefreshTokenHash ComputeHash(string token)
        {
                if (string.IsNullOrWhiteSpace(value: token))
                {
                        throw new ArgumentException(
                                message: TOKEN_REQUIRED,
                                paramName: nameof(token));
                }

                // Store only the SHA-256 hash, never the raw refresh token.
                byte[] tokenBytes = Encoding.UTF8.GetBytes(s: token);
                byte[] hashBytes = SHA256.HashData(source: tokenBytes);

                return RefreshTokenHash.FromHex(
                        value: Convert.ToHexString(inArray: hashBytes));
        }
}
