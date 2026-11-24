using ALKsPersonalUI.Core;

namespace ContentService.Components.Internal;

/// <summary>
/// Pure layout modifier builders, scope checks, and empty-state text formatting for <c>ContentImageGallery</c>.
/// </summary>
internal static class ContentGalleryLayout
{
        private const double SEARCH_MIN_WIDTH_PX = 240;
        private const double SEARCH_MAX_WIDTH_PX = 420;
        private const double SEARCH_FLEX_GROW = 1;
        private const double LOADER_MIN_HEIGHT_PX = 260;
        private const double EMPTY_STATE_MIN_HEIGHT_PX = 220;
        private const double EMPTY_STATE_PADDING_PX = 24;
        private const double EMPTY_STATE_RADIUS_PX = 12;
        private const double PAGINATION_VERTICAL_PADDING_PX = 8;
        private const double ZERO_PX = 0;

        /// <summary>
        /// Checks whether <paramref name="candidateAlbumPath"/> is equal to or nested beneath <paramref name="lockedRootAlbumPath"/>.
        /// </summary>
        /// <param name="candidateAlbumPath">The target album path being navigated to.</param>
        /// <param name="lockedRootAlbumPath">The normalized locked root album path, if any.</param>
        /// <returns><see langword="true"/> when within scope; otherwise <see langword="false"/>.</returns>
        public static bool IsWithinLockedScope(
                string? candidateAlbumPath,
                string? lockedRootAlbumPath)
        {
                if (lockedRootAlbumPath is null)
                {
                        return true;
                }

                if (candidateAlbumPath is null)
                {
                        return false;
                }

                // Append '/' when checking prefix so locked scope "Madaffald" never allows navigating into a sibling like "Madaffald2".
                return string.Equals(a: candidateAlbumPath, b: lockedRootAlbumPath, comparisonType: StringComparison.OrdinalIgnoreCase)
                        || candidateAlbumPath.StartsWith(value: $"{lockedRootAlbumPath}/", comparisonType: StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Resolves the empty-state message for the current search term or active album path.
        /// </summary>
        /// <param name="searchTerm">The active search query.</param>
        /// <param name="currentAlbumPath">The active album path, or <see langword="null"/> at root.</param>
        /// <returns>The user-facing empty-state message.</returns>
        public static string ResolveEmptyStateMessage(
                string searchTerm,
                string? currentAlbumPath)
        {
                if (!string.IsNullOrWhiteSpace(value: searchTerm))
                {
                        return $"Ingen billeder matcher '{searchTerm.Trim()}'.";
                }

                return string.IsNullOrWhiteSpace(value: currentAlbumPath)
                        ? "Der er ikke uploadet nogen udstillingsbilleder endnu."
                        : $"Ingen billeder i albummet '{currentAlbumPath}'.";
        }

        /// <summary>
        /// Builds the modifier applied to the gallery search input.
        /// </summary>
        public static UIModifier BuildSearchInputModifier() =>
                UIModifier.New
                        .MinWidth(px: SEARCH_MIN_WIDTH_PX)
                        .MaxWidth(px: SEARCH_MAX_WIDTH_PX)
                        .FlexGrow(growFactor: SEARCH_FLEX_GROW);

        /// <summary>
        /// Builds the modifier applied to the loading indicator container.
        /// </summary>
        public static UIModifier BuildLoaderBoxModifier() =>
                UIModifier.New.FillMaxWidth().MinHeight(px: LOADER_MIN_HEIGHT_PX);

        /// <summary>
        /// Builds the modifier applied to the empty-state container.
        /// </summary>
        public static UIModifier BuildEmptyStateBoxModifier() =>
                UIModifier.New
                        .FillMaxWidth()
                        .MinHeight(px: EMPTY_STATE_MIN_HEIGHT_PX)
                        .Padding(uniformPx: EMPTY_STATE_PADDING_PX)
                        .BorderRadius(px: EMPTY_STATE_RADIUS_PX);

        /// <summary>
        /// Builds the modifier applied to the pagination footer row.
        /// </summary>
        public static UIModifier BuildPaginationRowModifier() =>
                UIModifier.New
                        .FillMaxWidth()
                        .Padding(verticalPx: PAGINATION_VERTICAL_PADDING_PX, horizontalPx: ZERO_PX);
}

