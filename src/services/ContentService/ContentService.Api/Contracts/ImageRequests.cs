namespace ContentService.Api.Contracts;

/// <summary>
/// HTTP request contract for updating an image's searchable description (<c>AltText</c>), tags, and album path.
/// </summary>
public sealed record class UpdateImageMetadataRequest
{
        /// <summary>
        /// Gets the updated description / alternative text, or <see langword="null"/> to clear it.
        /// </summary>
        public string? AltText { get; init; }

        /// <summary>
        /// Gets the updated comma-separated search tags, or <see langword="null"/> to clear them.
        /// </summary>
        public string? Tags { get; init; }

        /// <summary>
        /// Gets the updated normalized recursive album path, or <see langword="null"/> for root.
        /// </summary>
        public string? AlbumPath { get; init; }

        /// <summary>
        /// Gets the expected concurrency stamp guarding against lost updates.
        /// </summary>
        public required Guid ExpectedConcurrencyStamp { get; init; }
}

