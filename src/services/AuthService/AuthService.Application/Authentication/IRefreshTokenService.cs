
using AuthService.Domain.ValueObjects;

namespace AuthService.Application.Authentication;

public interface IRefreshTokenService
{
        public IssuedRefreshToken Generate();

        public RefreshTokenHash ComputeHash(string token);
}
