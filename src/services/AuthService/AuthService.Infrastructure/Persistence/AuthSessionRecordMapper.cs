
using AuthService.Domain.Entities;
using AuthService.Domain.ValueObjects;
using AuthService.Infrastructure.Persistence.Entities;

namespace AuthService.Infrastructure.Persistence;

public static class AuthSessionRecordMapper
{
        public static AuthSession ToDomain(AuthSessionRecord record)
        {
                ArgumentNullException.ThrowIfNull(argument: record);

                // Reconstitution preserves the original expiration,
                // revocation and token history without creating a new session.
                return AuthSession.Restore(
                        id: AuthSessionId.From(value: record.Id),
                        userId: UserId.From(value: record.UserId),
                        createdAtUtc: FromStoredUtc(record.CreatedAtUtc),
                        lastUserActivityAtUtc: FromStoredUtc(
                                record.LastUserActivityAtUtc),
                        absoluteExpiresAtUtc: FromStoredUtc(
                                record.AbsoluteExpiresAtUtc),
                        idleTimeout: TimeSpan.FromTicks(
                                value: record.IdleTimeoutTicks),
                        revokedAtUtc: record.RevokedAtUtc is DateTime revoked
                                ? FromStoredUtc(revoked)
                                : null,
                        suspectedReplayDetectedAtUtc:
                                record.SuspectedReplayDetectedAtUtc
                                        is DateTime replay
                                ? FromStoredUtc(replay)
                                : null,
                        currentRefreshTokenHash:
                                record.CurrentRefreshTokenHash is string hash
                                ? RefreshTokenHash.FromHex(value: hash)
                                : null,
                        consumedRefreshTokenHashes:
                                record.ConsumedRefreshTokens.Select(
                                        token => RefreshTokenHash.FromHex(
                                                value: token.TokenHash)));
        }

        public static AuthSessionRecord ToNewRecord(AuthSession session)
        {
                ArgumentNullException.ThrowIfNull(argument: session);

                // Create a new persistence record with its initial
                // concurrency version. Existing records are updated separately.
                return new AuthSessionRecord
                {
                        Id = session.Id.Value,
                        UserId = session.UserId.Value,
                        CreatedAtUtc = session.CreatedAtUtc.UtcDateTime,
                        LastUserActivityAtUtc =
                                session.LastUserActivityAtUtc.UtcDateTime,
                        AbsoluteExpiresAtUtc =
                                session.AbsoluteExpiresAtUtc.UtcDateTime,
                        RevokedAtUtc = session.RevokedAtUtc?.UtcDateTime,
                        SuspectedReplayDetectedAtUtc =
                                session.SuspectedReplayDetectedAtUtc?.UtcDateTime,
                        IdleTimeoutTicks = session.IdleTimeout.Ticks,
                        CurrentRefreshTokenHash =
                                session.CurrentRefreshTokenHash?.Value,
                        Version = Guid.NewGuid(),
                        ConsumedRefreshTokens =
                                session.ConsumedRefreshTokenHashes.Select(
                                        hash => new ConsumedRefreshTokenRecord
                                        {
                                                AuthSessionId = session.Id.Value,
                                                TokenHash = hash.Value
                                        }).ToList()
                };
        }

        public static void ApplyToExistingRecord(
                AuthSession session,
                AuthSessionRecord record)
        {
                ArgumentNullException.ThrowIfNull(argument: session);
                ArgumentNullException.ThrowIfNull(argument: record);

                // Existing sessions must retain their identity and original lifetime.
                if (record.Id != session.Id.Value
                        || record.UserId != session.UserId.Value
                        || FromStoredUtc(record.CreatedAtUtc) != session.CreatedAtUtc
                        || FromStoredUtc(record.AbsoluteExpiresAtUtc)
                                != session.AbsoluteExpiresAtUtc
                        || record.IdleTimeoutTicks != session.IdleTimeout.Ticks)
                {
                        throw new InvalidOperationException(
                                message: "Persisted session identity or lifetime cannot be changed.");
                }

                record.LastUserActivityAtUtc =
                        session.LastUserActivityAtUtc.UtcDateTime;

                record.RevokedAtUtc = session.RevokedAtUtc?.UtcDateTime;

                record.SuspectedReplayDetectedAtUtc =
                        session.SuspectedReplayDetectedAtUtc?.UtcDateTime;

                record.CurrentRefreshTokenHash =
                        session.CurrentRefreshTokenHash?.Value;

                // Preserve consumed-token history to support future replay detection.
                HashSet<string> persistedHashes =
                        record.ConsumedRefreshTokens
                                .Select(token => token.TokenHash)
                                .ToHashSet(comparer: StringComparer.Ordinal);

                foreach (RefreshTokenHash hash in session.ConsumedRefreshTokenHashes)
                {
                        if (!persistedHashes.Add(item: hash.Value))
                        {
                                continue;
                        }

                        record.ConsumedRefreshTokens.Add(
                                item: new ConsumedRefreshTokenRecord
                                {
                                        AuthSessionId = record.Id,
                                        TokenHash = hash.Value
                                });
                }
        }


        private static DateTimeOffset FromStoredUtc(DateTime value)
        {
                // SQLite may return DateTime without Kind information.
                // Restore UTC explicitly to prevent incorrect time conversion.
                return new DateTimeOffset(
                        dateTime: DateTime.SpecifyKind(
                                value: value,
                                kind: DateTimeKind.Utc));
        }
}
