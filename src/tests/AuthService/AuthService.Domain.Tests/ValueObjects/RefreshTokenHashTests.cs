
using AuthService.Domain.ValueObjects;
using Xunit;

namespace AuthService.Domain.Tests.ValueObjects;

public sealed class RefreshTokenHashTests
{
        private const string VALID_HASH =
                "A1B2C3D4E5F60718293A4B5C6D7E8F90" +
                "A1B2C3D4E5F60718293A4B5C6D7E8F90";

        [Fact]
        public void FromHexAcceptsValidHash()
        {
                RefreshTokenHash hash = RefreshTokenHash.FromHex(
                        value: VALID_HASH);

                Assert.Equal(
                        expected: VALID_HASH,
                        actual: hash.Value);
        }

        [Fact]
        public void HashComparisonIgnoresHexadecimalCasing()
        {
                RefreshTokenHash first = RefreshTokenHash.FromHex(
                        value: VALID_HASH);

                RefreshTokenHash second = RefreshTokenHash.FromHex(
                        value: VALID_HASH.ToLowerInvariant());

                Assert.Equal(expected: first, actual: second);
        }

        [Theory]
        [InlineData("")]
        [InlineData("1234")]
        [InlineData(
                "ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ" +
                "ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
        public void FromHexRejectsInvalidHash(string value)
        {
                Assert.Throws<ArgumentException>(
                        testCode: () => RefreshTokenHash.FromHex(
                                value: value));
        }
}
