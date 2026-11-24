namespace ContentService.Facade.Dtos;

/// <summary>
/// Command DTO for archiving an existing exhibition template.
/// </summary>
public sealed record class DeleteTemplateCommandDto
{
        /// <summary>
        /// Gets the unique identifier of the template to archive.
        /// </summary>
        public required Guid TemplateId { get; init; }

        /// <summary>
        /// Gets the trusted actor identifier for audit attribution.
        /// </summary>
        public required string ActorId { get; init; }
}

