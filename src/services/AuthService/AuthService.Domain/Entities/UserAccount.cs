
using AuthService.Domain.Authorization;
using AuthService.Domain.ValueObjects;

namespace AuthService.Domain.Entities;

public sealed class UserAccount
{
        private const string ADMIN_PERMISSIONS_FIXED =
                "Administrator permissions cannot be overridden.";

        private readonly List<PermissionOverride> _permissionOverrides = [];

        public UserId Id { get; }
        public Username Username { get; private set; }
        public RoleProfile Role { get; private set; }
        public bool IsActive { get; private set; }

        public IReadOnlyCollection<PermissionOverride> PermissionOverrides =>
                this._permissionOverrides.AsReadOnly();

        public UserAccount(
                UserId id,
                Username username,
                RoleProfile role)
        {
                ArgumentNullException.ThrowIfNull(argument: id);
                ArgumentNullException.ThrowIfNull(argument: username);

                if (!Enum.IsDefined(value: role))
                {
                        throw new ArgumentOutOfRangeException(
                                paramName: nameof(role));
                }

                this.Id = id;
                this.Username = username;
                this.Role = role;
                this.IsActive = true;
        }

        public void Rename(Username username)
        {
                ArgumentNullException.ThrowIfNull(argument: username);

                this.Username = username;
        }

        public bool HasPermission(Permission permission)
        {
                if (!Enum.IsDefined(value: permission))
                {
                        throw new ArgumentOutOfRangeException(
                                paramName: nameof(permission));
                }

                if (!this.IsActive)
                {
                        return false;
                }

                PermissionOverride? permissionOverride =
                        this._permissionOverrides.FirstOrDefault(
                                predicate: item => item.Permission == permission);

                if (permissionOverride is not null)
                {
                        return permissionOverride.IsAllowed;
                }

                return DefaultPermissionPolicy.HasPermission(
                        role: this.Role,
                        permission: permission);
        }

        public void SetPermissionOverride(
                Permission permission,
                bool isAllowed)
        {
                if (!Enum.IsDefined(value: permission))
                {
                        throw new ArgumentOutOfRangeException(
                                paramName: nameof(permission));
                }

                if (this.Role == RoleProfile.Administrator)
                {
                        throw new InvalidOperationException(
                                message: ADMIN_PERMISSIONS_FIXED);
                }

                this._permissionOverrides.RemoveAll(
                        match: item => item.Permission == permission);

                this._permissionOverrides.Add(
                        item: new PermissionOverride(
                                Permission: permission,
                                IsAllowed: isAllowed));
        }

        public void ResetPermissionOverride(Permission permission)
        {
                if (!Enum.IsDefined(value: permission))
                {
                        throw new ArgumentOutOfRangeException(
                                paramName: nameof(permission));
                }

                this._permissionOverrides.RemoveAll(
                        match: item => item.Permission == permission);
        }
}
