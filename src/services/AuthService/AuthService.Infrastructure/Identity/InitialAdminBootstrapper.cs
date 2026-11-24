
using AuthService.Domain.Authorization;
using AuthService.Domain.ValueObjects;
using AuthService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.Identity;

public sealed class InitialAdminBootstrapper
{
        private const string ACCOUNTS_ALREADY_EXIST =
                "Bootstrap refused: User accounts already exist.";

        private const string PASSWORD_REQUIRED =
                "Bootstrap administrator password is required.";

        private const string CREATION_FAILED =
                "Administrator creation failed:";

        private readonly UserManager<AuthIdentityUser> _userManager;
        private readonly AuthDbContext _dbContext;

        public InitialAdminBootstrapper(
                UserManager<AuthIdentityUser> userManager,
                AuthDbContext dbContext)
        {
                this._userManager = userManager;
                this._dbContext = dbContext;
        }

        public async Task RunAsync(string username, string password)
        {
                Username validUsername = Username.Create(value: username);

                if (string.IsNullOrWhiteSpace(value: password))
                {
                        throw new ArgumentException(
                                message: PASSWORD_REQUIRED,
                                paramName: nameof(password));
                }

                // A transaction prevents concurrent bootstrap operations
                // from independently checking an empty database.
                await using var transaction =
                        await this._dbContext.Database.BeginTransactionAsync();

                if (await this._userManager.Users.AnyAsync())
                {
                        throw new InvalidOperationException(
                                message: ACCOUNTS_ALREADY_EXIST);
                }

                AuthIdentityUser administrator = new()
                {
                        Id = Guid.NewGuid(),
                        UserName = validUsername.Value,
                        Role = RoleProfile.Administrator,
                        IsActive = true
                };

                // Identity validates and hashes the password before persistence.
                IdentityResult result = await this._userManager.CreateAsync(
                        user: administrator,
                        password: password);

                if (!result.Succeeded)
                {
                        string errors = string.Join(
                                separator: "; ",
                                values: result.Errors.Select(
                                        error => error.Description));

                        throw new InvalidOperationException(
                                message: $"{CREATION_FAILED} {errors}");
                }

                await transaction.CommitAsync();
        }
}
