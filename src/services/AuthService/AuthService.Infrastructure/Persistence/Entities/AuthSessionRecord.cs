
namespace AuthService.Infrastructure.Persistence.Entities;

// EF Core persistence model, kept separate from the Domain Aggregate Root.
public sealed class AuthSessionRecord
{
        public Guid Id { get; set; }
        public Guid UserId { get; set; }

        // UTC storage avoids timezone-dependent session expiration.
        public DateTime CreatedAtUtc { get; set; }
        public DateTime LastUserActivityAtUtc { get; set; }
        public DateTime AbsoluteExpiresAtUtc { get; set; }
        public DateTime? RevokedAtUtc { get; set; }
        public DateTime? SuspectedReplayDetectedAtUtc { get; set; }

        public long IdleTimeoutTicks { get; set; }

        // Store only the token hash, never the raw refresh token.
        public string? CurrentRefreshTokenHash { get; set; }

        // SQLite requires application-managed optimistic concurrency.
        public Guid Version { get; set; }

        public List<ConsumedRefreshTokenRecord> ConsumedRefreshTokens
        {
                get;
                set;
        } = [];
}
