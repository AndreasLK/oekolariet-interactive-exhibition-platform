
using AuthService.Domain.Authorization;
using AuthService.Domain.Entities;
using AuthService.Domain.ValueObjects;

namespace AuthService.Domain.Tests.Authorization;

public sealed class PermissionPolicyTests
{
        // Theory tests multiple role scenarios against the same domain rule.
        [Theory]
        [InlineData(RoleProfile.Administrator, Permission.ManageAccounts, true)]
        [InlineData(RoleProfile.Staff, Permission.PublishGame, true)]
        [InlineData(RoleProfile.Staff, Permission.ManageAccounts, false)]
        [InlineData(RoleProfile.LimitedStaff, Permission.EditOwnDraft, true)]
        [InlineData(RoleProfile.LimitedStaff, Permission.PublishGame, false)]
        public void DefaultPermissionsMatchRolePolicy(
                RoleProfile role,
                Permission permission,
                bool expected)
        {
                bool actual = DefaultPermissionPolicy.HasPermission(
                        role: role,
                        permission: permission);

                Assert.Equal(expected: expected, actual: actual);
        }

        [Fact]
        public void ExplicitPermissionOverrideReplacesRoleDefault()
        {
                UserAccount account = new(
                        id: UserId.CreateNew(),
                        username: Username.Create(value: "TestStaff"),
                        role: RoleProfile.LimitedStaff);

                // An explicit override must take precedence over role defaults.
                account.SetPermissionOverride(
                        permission: Permission.PublishGame,
                        isAllowed: true);

                Assert.True(
                        condition: account.HasPermission(
                                permission: Permission.PublishGame));
        }

        [Fact]
        public void PermissionOverrideCanRemoveDefaultAccess()
        {
                UserAccount account = new(
                        id: UserId.CreateNew(),
                        username: Username.Create(value: "TestStaff"),
                        role: RoleProfile.Staff);

                // Deny overrides must also work for permissions granted by default.
                account.SetPermissionOverride(
                        permission: Permission.PublishGame,
                        isAllowed: false);

                Assert.False(
                        condition: account.HasPermission(
                                permission: Permission.PublishGame));
        }
}
