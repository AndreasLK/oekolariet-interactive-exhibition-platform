
using AuthService.Domain.Authorization;
using AuthService.Domain.Entities;
using AuthService.Domain.ValueObjects;
using AuthService.Infrastructure.Identity;
using Xunit;

namespace AuthService.Infrastructure.Tests.Identity;

public sealed class AuthAccountMapperTests
{
        private const string TEST_USERNAME = "TestStaff";

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ToDomainPreservesAccountStateAndPermissions(bool isActive)
        {
                Guid userId = Guid.NewGuid();

                AuthIdentityUser identityUser = new()
                {
                        Id = userId,
                        UserName = TEST_USERNAME,
                        Role = RoleProfile.LimitedStaff,
                        IsActive = isActive,
                        PermissionOverrides =
                        [
                                new AuthPermissionOverride
                                {
                                        UserId = userId,
                                        Permission = Permission.PublishGame,
                                        IsAllowed = true
                                }
                        ]
                };

                // The mapper translates persistence models into domain types.
                UserAccount account = AuthAccountMapper.ToDomain(
                        identityUser: identityUser);

                Assert.Equal(
                        expected: UserId.From(value: userId),
                        actual: account.Id);

                Assert.Equal(
                        expected: Username.Create(value: TEST_USERNAME),
                        actual: account.Username);

                Assert.Equal(
                        expected: RoleProfile.LimitedStaff,
                        actual: account.Role);

                Assert.Equal(
                        expected: isActive,
                        actual: account.IsActive);

                // A persisted permission never bypasses account deactivation.
                Assert.Equal(
                        expected: isActive,
                        actual: account.HasPermission(
                                permission: Permission.PublishGame));
        }
}
