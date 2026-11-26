namespace ContentService.Application.Models;

/// <summary>
/// Query criteria for listing immediate child album paths beneath an optional parent album.
/// </summary>
public sealed record class ListAlbumsQuery
{
        /// <summary>
        /// Gets the optional parent album path, or <see langword="null"/> for root-level albums.
        /// </summary>
        public string? ParentAlbumPath { get; init; }
}

