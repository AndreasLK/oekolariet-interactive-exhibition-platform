using ALKsPersonalUI.Core;
using ALKsPersonalUI.Navigation;
using ContentService.Components.Internal;
using Microsoft.AspNetCore.Components;

namespace ContentService.Components.Components;

/// <summary>
/// Controlled navigation bar rendering recursive album breadcrumbs and immediate child album pills.
/// </summary>
public sealed partial class ContentAlbumBreadcrumbBar : ComponentBase
{
        private const string ROOT_BREADCRUMB_ID = "__root__";
        private const string ROOT_BREADCRUMB_LABEL = "All Images";
        private const char ALBUM_SEPARATOR = '/';

        /// <summary>
        /// Gets or sets the currently active album path, or <see langword="null"/> for root.
        /// </summary>
        [Parameter]
        public string? CurrentAlbumPath { get; set; }

        /// <summary>
        /// Gets or sets the optional locked root album path (for host scopes such as <c>Madaffald</c>) preventing navigation above it.
        /// </summary>
        [Parameter]
        public string? LockedRootAlbumPath { get; set; }

        /// <summary>
        /// Gets or sets the sorted list of immediate sub-album paths beneath <see cref="CurrentAlbumPath"/>.
        /// </summary>
        [Parameter]
        public IReadOnlyList<string> ChildAlbumPaths { get; set; } = [];

        /// <summary>
        /// Gets or sets a value indicating whether nested sub-albums are included in the active gallery query.
        /// </summary>
        [Parameter]
        public bool IncludeSubalbums { get; set; } = true;

        /// <summary>
        /// Gets or sets the callback invoked when the user selects a breadcrumb or child album.
        /// </summary>
        [Parameter]
        public EventCallback<string?> OnNavigateAlbum { get; set; }

        /// <summary>
        /// Gets or sets the callback invoked when the user toggles recursive sub-album inclusion.
        /// </summary>
        [Parameter]
        public EventCallback<bool> OnIncludeSubalbumsChanged { get; set; }

        /// <summary>
        /// Builds clickable breadcrumb items from <see cref="CurrentAlbumPath"/> while hiding ancestor segments above <see cref="LockedRootAlbumPath"/>.
        /// </summary>
        private IReadOnlyList<Breadcrumbs.BreadcrumbItem> BuildBreadcrumbItems()
        {
                List<Breadcrumbs.BreadcrumbItem> items = [];
                string? normalizedLock = AlbumPathNormalizer.Normalize(rawAlbumPath: this.LockedRootAlbumPath);
                string? normalizedCurrent = AlbumPathNormalizer.Normalize(rawAlbumPath: this.CurrentAlbumPath);

                // Omit the global "All Images" root breadcrumb when a consuming host (e.g., EditorService) locks the gallery to an album subtree.
                if (normalizedLock is null)
                {
                        items.Add(item: new Breadcrumbs.BreadcrumbItem(Id: ROOT_BREADCRUMB_ID, Label: ROOT_BREADCRUMB_LABEL));
                }

                if (normalizedCurrent is null)
                {
                        return items;
                }

                string[] segments = normalizedCurrent.Split(separator: ALBUM_SEPARATOR, options: StringSplitOptions.RemoveEmptyEntries);
                int lockDepth = normalizedLock is null
                        ? 0
                        : normalizedLock.Split(separator: ALBUM_SEPARATOR, options: StringSplitOptions.RemoveEmptyEntries).Length;

                // Build cumulative paths (e.g., "Genbrug" then "Genbrug/Plastik") so clicking any breadcrumb navigates directly to that depth.
                string accumulatedPath = string.Empty;
                for (int index = 0; index < segments.Length; index++)
                {
                        accumulatedPath = index == 0 ? segments[index] : $"{accumulatedPath}{ALBUM_SEPARATOR}{segments[index]}";

                        // Skip ancestor breadcrumbs above the locked root while always displaying the locked root segment itself.
                        if (index + 1 < lockDepth)
                        {
                                continue;
                        }

                        items.Add(item: new Breadcrumbs.BreadcrumbItem(Id: accumulatedPath, Label: segments[index]));
                }

                return items;
        }

        /// <summary>
        /// Translates the synthetic <see cref="ROOT_BREADCRUMB_ID"/> sentinel back to <see langword="null"/> (root) before notifying the parent gallery.
        /// </summary>
        private Task HandleBreadcrumbClickedAsync(Breadcrumbs.BreadcrumbItem clickedItem)
        {
                string? targetPath = clickedItem.Id == ROOT_BREADCRUMB_ID ? null : clickedItem.Id;
                return this.OnNavigateAlbum.InvokeAsync(arg: targetPath);
        }

        private Task HandleChildAlbumClickedAsync(string albumPath) =>
                this.OnNavigateAlbum.InvokeAsync(arg: albumPath);

        private Task HandleToggleSubalbumsAsync() =>
                this.OnIncludeSubalbumsChanged.InvokeAsync(arg: !this.IncludeSubalbums);
}

