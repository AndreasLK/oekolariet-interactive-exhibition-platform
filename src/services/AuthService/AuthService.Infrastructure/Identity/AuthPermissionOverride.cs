
using AuthService.Domain.Authorization;

namespace AuthService.Infrastructure.Identity;

public sealed class AuthPermissionOverride
{
        public Guid UserId { get; set; }
        public Permission Permission { get; set; }
        public bool IsAllowed { get; set; }
}
