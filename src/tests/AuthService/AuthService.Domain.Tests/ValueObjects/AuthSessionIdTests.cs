
using AuthService.Domain.Entities;
using AuthService.Domain.ValueObjects;
using Xunit;

namespace AuthService.Domain.Tests.ValueObjects;

public sealed class AuthSessionIdTests
{
        [Fact]
        public void CreateNewGeneratesNonEmptyId()
        {
                AuthSessionId sessionId = AuthSessionId.CreateNew();

                Assert.NotEqual(
                        expected: Guid.Empty,
                        actual: sessionId.Value);
        }

        [Fact]
        public void FromRestoresExistingId()
        {
                Guid existingId = Guid.NewGuid();

                AuthSessionId sessionId = AuthSessionId.From(
                        value: existingId);

                Assert.Equal(
                        expected: existingId,
                        actual: sessionId.Value);
        }

        [Fact]
        public void FromRejectsEmptyId()
        {
                Assert.Throws<ArgumentException>(
                        testCode: () => AuthSessionId.From(
                                value: Guid.Empty));
        }

}
