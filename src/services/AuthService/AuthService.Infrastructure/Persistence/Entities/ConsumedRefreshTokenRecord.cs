
namespace AuthService.Infrastructure.Persistence.Entities;

// Retains previously used token hashes to detect refresh-token replay.
// Each record belongs to a specific authentication session.
public sealed class ConsumedRefreshTokenRecord
{
        public Guid AuthSessionId { get; set; }

        public string TokenHash { get; set; } = string.Empty;
}
