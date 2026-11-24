
using AuthService.Domain.Authorization;
using AuthService.Domain.Entities;
using AuthService.Domain.ValueObjects;

namespace AuthService.Infrastructure.Identity;

public static class AuthAccountMapper
{
        public static UserAccount ToDomain(AuthIdentityUser identityUser)
        {
                ArgumentNullException.ThrowIfNull(argument: identityUser);

                if (string.IsNullOrWhiteSpace(identityUser.UserName))
                {
                        throw new InvalidOperationException(
                                message: "Identity user must have a username.");
                }

                // Mapping prevents Identity and EF Core dependencies from leaking into Domain.
                return UserAccount.Restore(
                        id: UserId.From(value: identityUser.Id),
                        username: Username.Create(value: identityUser.UserName),
                        role: identityUser.Role,
                        isActive: identityUser.IsActive,
                        permissionOverrides: identityUser.PermissionOverrides.Select(
                                selector: item => new PermissionOverride(
                                        Permission: item.Permission,
                                        IsAllowed: item.IsAllowed)));
        }
}
