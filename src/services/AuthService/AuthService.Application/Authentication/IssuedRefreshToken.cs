
using AuthService.Domain.ValueObjects;

namespace AuthService.Application.Authentication;

public sealed class IssuedRefreshToken
{
        private const string REDACTED_TOKEN = "[REDACTED]";

        public string Value { get; }
        public RefreshTokenHash Hash { get; }

        public IssuedRefreshToken(
                string value,
                RefreshTokenHash hash)
        {
                if (string.IsNullOrWhiteSpace(value))
                {
                        throw new ArgumentException(
                                message: "Refresh token is required.",
                                paramName: nameof(value));
                }

                ArgumentNullException.ThrowIfNull(argument: hash);

                this.Value = value;
                this.Hash = hash;
        }

        // Prevent accidental exposure in logs or debugging output.
        public override string ToString()
        {
                return REDACTED_TOKEN;
        }
}
