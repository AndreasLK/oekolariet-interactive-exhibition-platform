
namespace AuthService.Domain.Authorization;

public sealed record PermissionOverride(
        Permission Permission,
        bool IsAllowed);
