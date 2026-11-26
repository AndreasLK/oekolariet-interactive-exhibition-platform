
namespace AuthService.Domain.ValueObjects;

public sealed record RefreshTokenHash
{
        private const int HASH_HEX_LENGTH = 64;

        private const string INVALID_HASH =
                "Refresh token hash must contain exactly 64 hexadecimal characters.";

        public string Value { get; }

        private RefreshTokenHash(string value)
        {
                this.Value = value;
        }

        public static RefreshTokenHash FromHex(string value)
        {
                if (string.IsNullOrWhiteSpace(value)
                        || value.Length != HASH_HEX_LENGTH
                        || !value.All(Uri.IsHexDigit))
                {
                        throw new ArgumentException(
                                message: INVALID_HASH,
                                paramName: nameof(value));
                }

                // Normalize the representation for consistent value equality.
                return new RefreshTokenHash(
                        value: value.ToUpperInvariant());
        }
}
