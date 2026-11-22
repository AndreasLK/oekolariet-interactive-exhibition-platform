namespace ContentService.Application.Models;

/// <summary>
/// Command payload for updating searchable description, tags, and album location on an active image.
/// </summary>
public sealed record class UpdateImageMetadataCommand
{
        /// <summary>
        /// Gets the unique identifier of the image to update.
        /// </summary>
        public required Guid ImageId { get; init; }

        /// <summary>
        /// Gets the updated description / alternative text, or <see langword="null"/> to clear.
        /// </summary>
        public string? AltText { get; init; }

        /// <summary>
        /// Gets the updated comma-separated search tags, or <see langword="null"/> to clear.
        /// </summary>
        public string? Tags { get; init; }

        /// <summary>
        /// Gets the updated normalized recursive album path, or <see langword="null"/> for root.
        /// </summary>
        public string? AlbumPath { get; init; }

        /// <summary>
        /// Gets the expected optimistic concurrency stamp prior to modification.
        /// </summary>
        public required Guid ExpectedConcurrencyStamp { get; init; }

        /// <summary>
        /// Gets the identifier of the actor performing the update.
        /// </summary>
        public required string ActorId { get; init; }
}

