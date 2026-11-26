
using AuthService.Domain.Authorization;
using AuthService.Domain.Entities;
using AuthService.Domain.ValueObjects;
using Xunit;

namespace AuthService.Domain.Tests.Entities;

public sealed class UserAccountTests
{
        private const string TEST_USERNAME = "TestStaff";

        [Fact]
        public void RestorePreservesPersistedAccountState()
        {
                UserId userId = UserId.CreateNew();
                Username username = Username.Create(value: TEST_USERNAME);

                // Reconstitution must preserve identity and persisted permissions.
                UserAccount account = UserAccount.Restore(
                        id: userId,
                        username: username,
                        role: RoleProfile.Staff,
                        isActive: true,
                        permissionOverrides:
                        [
                                new PermissionOverride(
                                        Permission: Permission.PublishGame,
                                        IsAllowed: false)
                        ]);

                Assert.Equal(expected: userId, actual: account.Id);
                Assert.Equal(expected: username, actual: account.Username);
                Assert.Equal(expected: RoleProfile.Staff, actual: account.Role);
                Assert.True(condition: account.IsActive);

                Assert.False(
                        condition: account.HasPermission(
                                permission: Permission.PublishGame));
        }

        [Fact]
        public void InactiveAccountCannotExerciseAdministratorPermissions()
        {
                UserAccount account = UserAccount.Restore(
                        id: UserId.CreateNew(),
                        username: Username.Create(value: TEST_USERNAME),
                        role: RoleProfile.Administrator,
                        isActive: false,
                        permissionOverrides: []);

                // Account status must take precedence over all granted permissions.
                Assert.False(
                        condition: account.HasPermission(
                                permission: Permission.ManageAccounts));
        }
}
