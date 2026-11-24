
using AuthService.Domain.Authorization;
using Microsoft.AspNetCore.Identity;

namespace AuthService.Infrastructure.Identity;

public sealed class AuthIdentityUser : IdentityUser<Guid>
{
        // Identity stores credentials, while domain rules remain in UserAccount.
        public RoleProfile Role { get; set; } = RoleProfile.LimitedStaff;

        public bool IsActive { get; set; } = true;

        // Persist individual permissions separately from the user's role defaults.
        public List<AuthPermissionOverride> PermissionOverrides { get; set; } = [];
}
