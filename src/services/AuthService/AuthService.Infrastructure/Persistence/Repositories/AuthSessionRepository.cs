
using AuthService.Application.Sessions;
using AuthService.Domain.Entities;
using AuthService.Domain.ValueObjects;
using AuthService.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.Persistence.Repositories;

public sealed class AuthSessionRepository : IAuthSessionRepository
{
        private readonly AuthDbContext _dbContext;

        public AuthSessionRepository(AuthDbContext dbContext)
        {
                this._dbContext = dbContext;
        }

        public async Task<VersionedAuthSession?> FindByIdAsync(
                AuthSessionId sessionId,
                CancellationToken cancellationToken = default)
        {
                ArgumentNullException.ThrowIfNull(argument: sessionId);

                // Include token history so Domain can detect previously used tokens.
                AuthSessionRecord? record = await this._dbContext.AuthSessions
                        .AsNoTracking()
                        .Include(session => session.ConsumedRefreshTokens)
                        .SingleOrDefaultAsync(
                                predicate: session => session.Id == sessionId.Value,
                                cancellationToken: cancellationToken);

                return record is null
                        ? null
                        : new VersionedAuthSession(
                                Session: AuthSessionRecordMapper.ToDomain(record),
                                Version: record.Version);
        }

        public async Task<VersionedAuthSession?> FindByRefreshTokenHashAsync(
                RefreshTokenHash tokenHash,
                CancellationToken cancellationToken = default)
        {
                ArgumentNullException.ThrowIfNull(argument: tokenHash);

                // Search both current and consumed hashes to identify token replay.
                AuthSessionRecord? record = await this._dbContext.AuthSessions
                        .AsNoTracking()
                        .Include(session => session.ConsumedRefreshTokens)
                        .SingleOrDefaultAsync(
                                predicate: session =>
                                        session.CurrentRefreshTokenHash == tokenHash.Value
                                        || session.ConsumedRefreshTokens.Any(
                                                token => token.TokenHash == tokenHash.Value),
                                cancellationToken: cancellationToken);

                return record is null
                        ? null
                        : new VersionedAuthSession(
                                Session: AuthSessionRecordMapper.ToDomain(record),
                                Version: record.Version);
        }

        public async Task AddAsync(
                AuthSession session,
                CancellationToken cancellationToken = default)
        {
                ArgumentNullException.ThrowIfNull(argument: session);

                AuthSessionRecord record =
                        AuthSessionRecordMapper.ToNewRecord(session: session);

                await this._dbContext.AuthSessions.AddAsync(
                        entity: record,
                        cancellationToken: cancellationToken);

                await this._dbContext.SaveChangesAsync(
                        cancellationToken: cancellationToken);
        }

        public async Task UpdateAsync(
                VersionedAuthSession session,
                CancellationToken cancellationToken = default)
        {
                ArgumentNullException.ThrowIfNull(argument: session);

                AuthSessionRecord? record = await this._dbContext.AuthSessions
                        .Include(item => item.ConsumedRefreshTokens)
                        .SingleOrDefaultAsync(
                                predicate: item => item.Id == session.Session.Id.Value,
                                cancellationToken: cancellationToken);

                if (record is null)
                {
                        throw new DbUpdateConcurrencyException(
                                message: "Authentication session no longer exists.");
                }

                AuthSessionRecordMapper.ApplyToExistingRecord(
                        session: session.Session,
                        record: record);

                // Compare against the version originally read by Application,
                // not the potentially newer version fetched by this DbContext.
                this._dbContext.Entry(record)
                        .Property(item => item.Version)
                        .OriginalValue = session.Version;

                // Force a versioned UPDATE even when only token history changes.
                record.Version = Guid.NewGuid();

                // SaveChanges atomically persists the session and token history.
                await this._dbContext.SaveChangesAsync(
                        cancellationToken: cancellationToken);
        }
}
