
using AuthService.Domain.Entities;

namespace AuthService.Application.Authentication;

public interface IAccessTokenIssuer
{
        // DIP: Application defines the contract; Infrastructure implements JWT signing.
        public IssuedAccessToken Issue(UserAccount account);
}
