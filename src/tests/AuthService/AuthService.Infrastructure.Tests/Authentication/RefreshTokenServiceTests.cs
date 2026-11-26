
using AuthService.Application.Authentication;
using AuthService.Domain.ValueObjects;
using AuthService.Infrastructure.Authentication;
using Xunit;

namespace AuthService.Infrastructure.Tests.Authentication;

public sealed class RefreshTokenServiceTests
{
        private const int EXPECTED_TOKEN_LENGTH = 43;

        [Fact]
        public void GenerateCreatesUniqueTokens()
        {
                RefreshTokenService service = new();

                IssuedRefreshToken first = service.Generate();
                IssuedRefreshToken second = service.Generate();

                Assert.NotEqual(
                        expected: first.Value,
                        actual: second.Value);

                Assert.NotEqual(
                        expected: first.Hash,
                        actual: second.Hash);

                Assert.Equal(
                        expected: EXPECTED_TOKEN_LENGTH,
                        actual: first.Value.Length);
        }

        [Fact]
        public void GeneratedHashMatchesComputedHash()
        {
                RefreshTokenService service = new();

                IssuedRefreshToken issued = service.Generate();

                RefreshTokenHash computed = service.ComputeHash(
                        token: issued.Value);

                Assert.Equal(
                        expected: issued.Hash,
                        actual: computed);
        }

        [Fact]
        public void TokenIsRedactedWhenConvertedToString()
        {
                RefreshTokenService service = new();

                IssuedRefreshToken issued = service.Generate();

                Assert.Equal(
                        expected: "[REDACTED]",
                        actual: issued.ToString());
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        public void ComputeHashRejectsInvalidToken(string token)
        {
                RefreshTokenService service = new();

                Assert.Throws<ArgumentException>(
                        testCode: () => service.ComputeHash(
                                token: token));
        }
}
