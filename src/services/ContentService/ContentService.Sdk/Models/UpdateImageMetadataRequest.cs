using System.Text.Json.Serialization;

namespace ContentService.Sdk.Models;

/// <summary>
/// Parameters required to update an active image's searchable description (<c>AltText</c>), tags, and album path.
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
        /// Gets the expected optimistic concurrency stamp prior to modification.
        /// </summary>
        /// <remarks>
        /// Must match the latest <see cref="ContentImageMetadata.ConcurrencyStamp"/> loaded from ContentService; otherwise the API returns HTTP 409 Conflict
        /// so two editors cannot silently overwrite each other's changes.
        /// </remarks>
        public required Guid ExpectedConcurrencyStamp { get; init; }

        /// <summary>
        /// Gets an optional audit actor identifier overriding <see cref="Options.ContentServiceClientOptions.DefaultActorId"/>.
        /// </summary>
        /// <remarks>
        /// Excluded from the JSON body (<see cref="JsonIgnoreAttribute"/>) because the SDK sends actor identity via the <c>X-Actor-Id</c> HTTP header.
        /// </remarks>
        [JsonIgnore]
        public string? ActorId { get; init; }
}

