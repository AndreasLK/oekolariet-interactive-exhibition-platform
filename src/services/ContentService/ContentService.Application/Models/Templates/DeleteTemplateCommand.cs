namespace ContentService.Application.Models;

/// <summary>
/// Command payload for archiving an existing exhibition template.
/// </summary>
public sealed record class DeleteTemplateCommand
{
        /// <summary>
        /// Gets the unique identifier of the template to archive.
        /// </summary>
        public required Guid TemplateId { get; init; }

        /// <summary>
        /// Gets the identifier of the actor archiving the template for audit logging.
        /// </summary>
        public required string ActorId { get; init; }
}

