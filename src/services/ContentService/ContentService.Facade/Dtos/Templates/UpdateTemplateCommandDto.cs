namespace ContentService.Facade.Dtos;

/// <summary>
/// Command DTO for updating an existing template's name and schema JSON with optimistic concurrency checking.
/// </summary>
public sealed record class UpdateTemplateCommandDto
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
        /// Gets the trusted actor identifier for audit attribution.
        /// </summary>
        public required string ActorId { get; init; }
}

