namespace ContentService.Facade.Dtos;

/// <summary>
/// Command DTO for soft-deleting an unreferenced exhibition image.
/// </summary>
public sealed record class DeleteImageCommandDto
{
        /// <summary>
        /// Gets the unique identifier of the image to soft-delete.
        /// </summary>
        public required Guid ImageId { get; init; }

        /// <summary>
        /// Gets the trusted actor identifier for audit attribution.
        /// </summary>
        public required string ActorId { get; init; }
}

