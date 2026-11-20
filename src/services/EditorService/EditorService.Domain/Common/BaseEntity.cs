using System;
using System.Collections.Generic;
using System.Text;
using EditorService.Domain.Exceptions;

namespace EditorService.Domain.Common
{
        public abstract class BaseEntity
        {
                /// <summary>
                /// Initializes a new instance of the <see cref="BaseEntity"/> class for ORM materialization.
                /// </summary>
                protected BaseEntity()
                {
                }

                /// <summary>Gets the unique identifier of the entity.</summary>
                public Guid Id { get; protected set; }

                /// <summary>Gets the UTC timestamp when the entity was created.</summary>
                public DateTimeOffset CreatedAt { get; protected set; }

                /// <summary>Gets the UTC timestamp when the entity was last modified.</summary>
                public DateTimeOffset UpdatedAt { get; protected set; }

                /// <summary>Gets the identifier of the actor who created the entity.</summary>
                public string CreatedBy { get; protected set; } = string.Empty;

                /// <summary>Gets the identifier of the actor who last modified the entity.</summary>
                public string UpdatedBy { get; protected set; } = string.Empty;

                /// <summary>Gets the optimistic concurrency token updated on every state mutation.</summary>
                public Guid ConcurrencyStamp { get; protected set; }

                /// <summary>
                /// Initializes the entity identity, creation audit metadata, and concurrency stamp.
                /// </summary>
                /// <param name="actorId">The identifier of the actor creating the entity.</param>
                /// <param name="now">The current UTC time.</param>
                protected void InitializeAudit(string actorId, DateTimeOffset now)
                {
                        string validatedActorId = ValidateActorId(actorId: actorId);

                        this.Id = Guid.NewGuid();
                        this.CreatedAt = now;
                        this.UpdatedAt = now;
                        this.CreatedBy = validatedActorId;
                        this.UpdatedBy = validatedActorId;
                        this.ConcurrencyStamp = Guid.NewGuid();
                }

                /// <summary>
                /// Updates the modification audit metadata and rotates the concurrency stamp.
                /// </summary>
                /// <param name="actorId">The identifier of the actor modifying the entity.</param>
                /// <param name="now">The current UTC time.</param>
                protected void MarkModified(string actorId, DateTimeOffset now)
                {
                        string validatedActorId = ValidateActorId(actorId: actorId);

                        this.UpdatedAt = now;
                        this.UpdatedBy = validatedActorId;
                        this.ConcurrencyStamp = Guid.NewGuid();
                }

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
}
