
using AuthService.Domain.Entities;

namespace AuthService.Application.Sessions;

// Carries the original database version alongside the Domain aggregate.
// Infrastructure uses this version to detect concurrent modifications.
public sealed record VersionedAuthSession(
        AuthSession Session,
        Guid Version);
