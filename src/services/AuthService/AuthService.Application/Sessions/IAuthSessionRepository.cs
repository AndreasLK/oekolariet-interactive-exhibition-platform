
using AuthService.Domain.Entities;
using AuthService.Domain.ValueObjects;

namespace AuthService.Application.Sessions;

// DIP: Application defines persistence operations without depending on EF Core.
// Infrastructure implements the contract and enforces database concurrency.
public interface IAuthSessionRepository
{
        public Task<VersionedAuthSession?> FindByIdAsync(
                AuthSessionId sessionId,
                CancellationToken cancellationToken = default);

        // Search both current and consumed hashes to support replay detection.
        public Task<VersionedAuthSession?> FindByRefreshTokenHashAsync(
                RefreshTokenHash tokenHash,
                CancellationToken cancellationToken = default);

        public Task AddAsync(
                AuthSession session,
                CancellationToken cancellationToken = default);

        // Reject stale versions to prevent concurrent token rotations.
        public Task UpdateAsync(
                VersionedAuthSession session,
                CancellationToken cancellationToken = default);
}
