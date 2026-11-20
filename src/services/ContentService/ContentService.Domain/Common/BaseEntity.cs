using ContentService.Domain.Exceptions;

namespace ContentService.Domain.Common;

/// <summary>
/// Provides shared identity, UTC audit metadata, and optimistic concurrency tracking for ContentService aggregate roots.
/// </summary>
// An abstract base class is used instead of an interface so every aggregate root inherits identical audit and concurrency behavior without duplicating code.
public abstract class BaseEntity
{
        /// <summary>
        /// Initializes a new instance of the <see cref="BaseEntity"/> class for EF Core.
        /// </summary>
        protected BaseEntity()
        {
        }

        /// <summary>
        /// Gets the unique domain identifier for this entity.
        /// </summary>
        // Generated in C# via Guid.NewGuid() rather than by the database so entities can be linked in memory before SaveChanges().
        public Guid Id { get; protected set; }

        /// <summary>
        /// Gets the UTC timestamp when this entity was first created.
        /// </summary>
        // Uses DateTimeOffset instead of DateTime so timestamps always carry explicit UTC offset information across servers and kiosks.
        public DateTimeOffset CreatedAt { get; protected set; }

        /// <summary>
        /// Gets the UTC timestamp when this entity was last modified.
        /// </summary>
        public DateTimeOffset UpdatedAt { get; protected set; }

        /// <summary>
        /// Gets the identifier of the actor that created this entity.
        /// </summary>
        // Initialized to string.Empty so EF Core materialization satisfies nullable checks while InitializeAudit guarantees a real value before saving.
        public string CreatedBy { get; protected set; } = string.Empty;

        /// <summary>
        /// Gets the identifier of the actor that most recently modified this entity.
        /// </summary>
        public string UpdatedBy { get; protected set; } = string.Empty;

        /// <summary>
        /// Gets the optimistic concurrency token rotated on creation and every modification.
        /// </summary>
        // Detects if two users edit the same record concurrently so the second save fails instead of silently overwriting the first.
        // Stored as a domain-managed Guid rather than SQL Server rowversion to keep concurrency checks persistence-agnostic.
        public Guid ConcurrencyStamp { get; protected set; }

        /// <summary>
        /// Initializes identity, creation/modification timestamps, actor audit fields, and the initial concurrency token.
        /// </summary>
        /// <param name="actorId">The identifier of the user or service creating the entity.</param>
        /// <exception cref="DomainValidationException">Thrown when <paramref name="actorId"/> is blank or exceeds <see cref="DomainConstants.MAX_ACTOR_ID_LENGTH"/>.</exception>
        // Kept separate from the constructor so EF Core retains a parameterless constructor and factories can validate all fields before stamping audit data.
        protected void InitializeAudit(string actorId)
        {
                string validatedActorId = ValidateActorId(actorId);
                DateTimeOffset now = DateTimeOffset.UtcNow;

                this.Id = Guid.NewGuid();
                this.CreatedAt = now;
                this.UpdatedAt = now;
                this.CreatedBy = validatedActorId;
                this.UpdatedBy = validatedActorId;
                this.ConcurrencyStamp = Guid.NewGuid();
        }

        /// <summary>
        /// Updates modification audit metadata and rotates <see cref="ConcurrencyStamp"/> to guard against stale concurrent writes.
        /// </summary>
        /// <param name="actorId">The identifier of the user or service modifying the entity.</param>
        /// <exception cref="DomainValidationException">Thrown when <paramref name="actorId"/> is blank or exceeds <see cref="DomainConstants.MAX_ACTOR_ID_LENGTH"/>.</exception>
        protected void MarkModified(string actorId)
        {
                string validatedActorId = ValidateActorId(actorId);

                this.UpdatedAt = DateTimeOffset.UtcNow;
                this.UpdatedBy = validatedActorId;
                this.ConcurrencyStamp = Guid.NewGuid();
        }

        /// <summary>
        /// Validates and trims an actor identifier before storing it in audit properties.
        /// </summary>
        /// <param name="actorId">The raw actor identifier supplied by the caller.</param>
        /// <returns>The trimmed, non-empty actor identifier within <see cref="DomainConstants.MAX_ACTOR_ID_LENGTH"/>.</returns>
        /// <exception cref="DomainValidationException">Thrown when <paramref name="actorId"/> is null, whitespace, or exceeds the maximum length after trimming.</exception>
        private static string ValidateActorId(string actorId)
        {
                // Throws DomainValidationException instead of ArgumentException so API error middleware maps all domain failures consistently.
                if (string.IsNullOrWhiteSpace(actorId))
                {
                        throw new DomainValidationException("Actor ID cannot be null or whitespace.");
                }

                string trimmedActorId = actorId.Trim();
                if (trimmedActorId.Length > DomainConstants.MAX_ACTOR_ID_LENGTH)
                {
                        throw new DomainValidationException(
                                $"Actor ID cannot exceed {DomainConstants.MAX_ACTOR_ID_LENGTH} characters.");
                }

                return trimmedActorId;
        }
}
