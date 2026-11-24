namespace ContentService.Application.Models;

/// <summary>
/// Command payload for updating an existing template's name and schema JSON with optimistic concurrency checking.
/// </summary>
/// <remarks>
/// <c>ComponentType</c> is intentionally omitted because <see cref="Domain.Entities.TemplateEntity"/> freezes it at creation to avoid breaking authored drafts.
/// </remarks>
public sealed record class UpdateTemplateCommand
{
        /// <summary>
        /// Gets the unique identifier of the template to update.
        /// </summary>
        public required Guid TemplateId { get; init; }

        /// <summary>
        /// Gets the updated display name for the template.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Gets the updated raw JSON schema payload.
        /// </summary>
        public required string SchemaJson { get; init; }

        /// <summary>
        /// Gets the expected concurrency stamp observed when the template was loaded.
        /// </summary>
        public required Guid ExpectedConcurrencyStamp { get; init; }

        /// <summary>
        /// Gets the identifier of the actor modifying the template for audit logging.
        /// </summary>
        public required string ActorId { get; init; }
}

