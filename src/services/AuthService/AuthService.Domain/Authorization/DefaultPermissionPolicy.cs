
namespace AuthService.Domain.Authorization;

public static class DefaultPermissionPolicy
{
        public static bool HasPermission(
                RoleProfile role,
                Permission permission)
        {
                if (!Enum.IsDefined(value: role))
                {
                        throw new ArgumentOutOfRangeException(
                                paramName: nameof(role));
                }

                if (!Enum.IsDefined(value: permission))
                {
                        throw new ArgumentOutOfRangeException(
                                paramName: nameof(permission));
                }

                return role switch
                {
                        RoleProfile.Administrator => true,

                        RoleProfile.Staff => permission is
                                Permission.CreateGame
                                or Permission.EditOwnDraft
                                or Permission.EditAnyDraft
                                or Permission.PublishGame
                                or Permission.DeleteGame,

                        RoleProfile.LimitedStaff => permission is
                                Permission.CreateGame
                                or Permission.EditOwnDraft,

                        _ => false
                };
        }
}
