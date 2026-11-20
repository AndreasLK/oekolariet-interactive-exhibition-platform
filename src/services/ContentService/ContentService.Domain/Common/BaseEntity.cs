using ContentService.Domain.Exceptions;

namespace ContentService.Domain.Common;

// Shared base class for all domain entities (SRP): centralizes identity, UTC audit timestamps, and optimistic concurrency tracking.
public abstract class BaseEntity
{
        // Parameterless protected constructor is required by EF Core to materialize rows from MSSQL while preventing direct 'new BaseEntity()' calls.
        protected BaseEntity()
        {
        }

        public Guid Id { get; protected set; }

        public DateTimeOffset CreatedAt { get; protected set; }

        public DateTimeOffset UpdatedAt { get; protected set; }

        public string CreatedBy { get; protected set; } = string.Empty;

        public string UpdatedBy { get; protected set; } = string.Empty;

        // Optimistic concurrency token: rotated on every save so two simultaneous editors cannot silently overwrite each other's changes.
        public Guid ConcurrencyStamp { get; protected set; }

        // Sets initial identity, UTC timestamps (to avoid timezone/DST drift), and the first concurrency token when an entity is created.
        protected void InitializeAudit(string actorId)
        {
                string validatedActorId = ValidateActorId(actorId: actorId);
                DateTimeOffset now = DateTimeOffset.UtcNow;

                this.Id = Guid.NewGuid();
                this.CreatedAt = now;
                this.UpdatedAt = now;
                this.CreatedBy = validatedActorId;
                this.UpdatedBy = validatedActorId;
                this.ConcurrencyStamp = Guid.NewGuid();
        }

        // Updates modification metadata and rotates ConcurrencyStamp so EF Core can detect stale writes.
        protected void MarkModified(string actorId)
        {
                string validatedActorId = ValidateActorId(actorId: actorId);

                this.UpdatedAt = DateTimeOffset.UtcNow;
                this.UpdatedBy = validatedActorId;
                this.ConcurrencyStamp = Guid.NewGuid();
        }

        // Trims and bounds-checks actorId so audit columns never store blank strings or exceed MSSQL column limits.
        private static string ValidateActorId(string actorId)
        {
                if (string.IsNullOrWhiteSpace(value: actorId))
                {
                        throw new DomainValidationException(message: "Actor ID cannot be null or whitespace.");
                }

                string trimmedActorId = actorId.Trim();
                if (trimmedActorId.Length > DomainConstants.MAX_ACTOR_ID_LENGTH)
                {
                        throw new DomainValidationException(
                                message: $"Actor ID cannot exceed {DomainConstants.MAX_ACTOR_ID_LENGTH} characters.");
                }

                return trimmedActorId;
        }
}
