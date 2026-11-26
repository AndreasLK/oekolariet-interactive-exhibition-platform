
using AuthService.Domain.ValueObjects;

namespace AuthService.Domain.Entities;

public sealed class AuthSession
{
        private const string INVALID_LIFETIME =
                "Session timeouts must be positive, and idle timeout must be shorter than absolute timeout.";

        private const string INACTIVE_SESSION =
                "Cannot record activity for an inactive session.";

        public AuthSessionId Id { get; }
        public UserId UserId { get; }
        public DateTimeOffset CreatedAtUtc { get; }
        public DateTimeOffset LastUserActivityAtUtc { get; private set; }
        public DateTimeOffset AbsoluteExpiresAtUtc { get; }
        public DateTimeOffset? RevokedAtUtc { get; private set; }
        public TimeSpan IdleTimeout { get; }

        private AuthSession(
                AuthSessionId id,
                UserId userId,
                DateTimeOffset nowUtc,
                TimeSpan idleTimeout,
                TimeSpan absoluteTimeout)
        {
                ArgumentNullException.ThrowIfNull(argument: id);
                ArgumentNullException.ThrowIfNull(argument: userId);

                if (idleTimeout <= TimeSpan.Zero
                        || absoluteTimeout <= TimeSpan.Zero
                        || idleTimeout >= absoluteTimeout)
                {
                        throw new ArgumentOutOfRangeException(
                                paramName: nameof(idleTimeout),
                                message: INVALID_LIFETIME);
                }

                DateTimeOffset createdAtUtc = nowUtc.ToUniversalTime();

                this.Id = id;
                this.UserId = userId;
                this.CreatedAtUtc = createdAtUtc;
                this.LastUserActivityAtUtc = createdAtUtc;
                this.IdleTimeout = idleTimeout;
                this.AbsoluteExpiresAtUtc =
                        createdAtUtc.Add(absoluteTimeout);
        }

        public static AuthSession Create(
                AuthSessionId id,
                UserId userId,
                DateTimeOffset nowUtc,
                TimeSpan idleTimeout,
                TimeSpan absoluteTimeout)
        {
                return new AuthSession(
                        id: id,
                        userId: userId,
                        nowUtc: nowUtc,
                        idleTimeout: idleTimeout,
                        absoluteTimeout: absoluteTimeout);
        }

        public bool IsActiveAt(DateTimeOffset nowUtc)
        {
                DateTimeOffset now = nowUtc.ToUniversalTime();

                return this.RevokedAtUtc is null
                        && now >= this.CreatedAtUtc
                        && now < this.AbsoluteExpiresAtUtc
                        && now < this.LastUserActivityAtUtc.Add(
                                this.IdleTimeout);
        }

        public void RecordUserActivity(DateTimeOffset nowUtc)
        {
                DateTimeOffset now = nowUtc.ToUniversalTime();

                if (!this.IsActiveAt(nowUtc: now))
                {
                        throw new InvalidOperationException(
                                message: INACTIVE_SESSION);
                }

                // Only verified user activity may extend the idle window.
                if (now > this.LastUserActivityAtUtc)
                {
                        this.LastUserActivityAtUtc = now;
                }
        }

        public void Revoke(DateTimeOffset nowUtc)
        {
                DateTimeOffset now = nowUtc.ToUniversalTime();

                if (now < this.CreatedAtUtc)
                {
                        throw new ArgumentOutOfRangeException(
                                paramName: nameof(nowUtc));
                }

                // Revocation is permanent and idempotent.
                this.RevokedAtUtc ??= now;
        }

        private readonly HashSet<RefreshTokenHash> _consumedRefreshTokenHashes = [];

        public RefreshTokenHash? CurrentRefreshTokenHash { get; private set; }

        public IReadOnlyCollection<RefreshTokenHash> ConsumedRefreshTokenHashes =>
                this._consumedRefreshTokenHashes.ToArray();
        public DateTimeOffset? SuspectedReplayDetectedAtUtc { get; private set; }

        public void RegisterInitialRefreshToken(
                RefreshTokenHash tokenHash,
                DateTimeOffset nowUtc)
        {
                ArgumentNullException.ThrowIfNull(argument: tokenHash);

                if (!this.IsActiveAt(nowUtc: nowUtc)
                        || this.CurrentRefreshTokenHash is not null)
                {
                        throw new InvalidOperationException(
                                message: "Cannot initialize the refresh token.");
                }

                this.CurrentRefreshTokenHash = tokenHash;
        }

        public bool IsCurrentRefreshToken(RefreshTokenHash tokenHash)
        {
                ArgumentNullException.ThrowIfNull(argument: tokenHash);

                return this.CurrentRefreshTokenHash == tokenHash;
        }

        public bool IsKnownConsumedRefreshToken(RefreshTokenHash tokenHash)
        {
                ArgumentNullException.ThrowIfNull(argument: tokenHash);

                return this._consumedRefreshTokenHashes.Contains(tokenHash);
        }

        public void RotateRefreshToken(
                RefreshTokenHash presentedHash,
                RefreshTokenHash replacementHash,
                DateTimeOffset nowUtc)
        {
                ArgumentNullException.ThrowIfNull(argument: presentedHash);
                ArgumentNullException.ThrowIfNull(argument: replacementHash);

                if (!this.IsActiveAt(nowUtc: nowUtc)
                        || this.CurrentRefreshTokenHash != presentedHash
                        || presentedHash == replacementHash
                        || this._consumedRefreshTokenHashes.Contains(replacementHash))
                {
                        throw new InvalidOperationException(
                                message: "Refresh token rotation is not allowed.");
                }

                // Retain consumed hashes to detect token reuse.
                this._consumedRefreshTokenHashes.Add(presentedHash);
                this.CurrentRefreshTokenHash = replacementHash;
        }

        public void RevokeForRefreshTokenReplay(
                RefreshTokenHash presentedHash,
                DateTimeOffset nowUtc)
        {
                ArgumentNullException.ThrowIfNull(argument: presentedHash);

                if (!this.IsKnownConsumedRefreshToken(tokenHash: presentedHash))
                {
                        throw new InvalidOperationException(
                                message: "Refresh token replay has not been established.");
                }

                // Revoke only this session and record the suspected replay.
                this.Revoke(nowUtc: nowUtc);

                this.SuspectedReplayDetectedAtUtc ??=
                        nowUtc.ToUniversalTime();
        }

        public static AuthSession Restore(
                AuthSessionId id,
                UserId userId,
                DateTimeOffset createdAtUtc,
                DateTimeOffset lastUserActivityAtUtc,
                DateTimeOffset absoluteExpiresAtUtc,
                TimeSpan idleTimeout,
                DateTimeOffset? revokedAtUtc,
                DateTimeOffset? suspectedReplayDetectedAtUtc,
                RefreshTokenHash? currentRefreshTokenHash,
                IEnumerable<RefreshTokenHash> consumedRefreshTokenHashes)
        {
                ArgumentNullException.ThrowIfNull(
                        argument: consumedRefreshTokenHashes);

                DateTimeOffset created = createdAtUtc.ToUniversalTime();
                DateTimeOffset lastActivity =
                        lastUserActivityAtUtc.ToUniversalTime();
                DateTimeOffset expires =
                        absoluteExpiresAtUtc.ToUniversalTime();

                if (lastActivity < created || lastActivity >= expires)
                {
                        throw new ArgumentException(
                                message: "Invalid persisted session timestamps.");
                }

                if (revokedAtUtc is not null
                        && revokedAtUtc.Value.ToUniversalTime() < created)
                {
                        throw new ArgumentException(
                                message: "Invalid persisted revocation timestamp.");
                }

                if (suspectedReplayDetectedAtUtc is not null
                        && (revokedAtUtc is null
                            || suspectedReplayDetectedAtUtc.Value.ToUniversalTime() < created))
                {
                        throw new ArgumentException(
                                message: "Invalid persisted replay state.");
                }

                RefreshTokenHash[] consumed =
                        consumedRefreshTokenHashes.ToArray();

                if ((currentRefreshTokenHash is null && consumed.Length > 0)
                        || (currentRefreshTokenHash is not null
                            && consumed.Contains(currentRefreshTokenHash))
                        || consumed.Distinct().Count() != consumed.Length)
                {
                        throw new ArgumentException(
                                message: "Invalid persisted refresh token state.");
                }

                // Reconstitution preserves the original absolute deadline.
                AuthSession session = new(
                        id: id,
                        userId: userId,
                        nowUtc: created,
                        idleTimeout: idleTimeout,
                        absoluteTimeout: expires - created);

                session.LastUserActivityAtUtc = lastActivity;
                session.RevokedAtUtc = revokedAtUtc?.ToUniversalTime();
                session.SuspectedReplayDetectedAtUtc =
                        suspectedReplayDetectedAtUtc?.ToUniversalTime();

                session.CurrentRefreshTokenHash = currentRefreshTokenHash;

                session._consumedRefreshTokenHashes.UnionWith(
                        other: consumed);

                return session;
        }

}
