using ALKsPersonalUI.Core;
using ALKsPersonalUI.Overlays;
using ContentService.Components.Internal;
using ContentService.Sdk.Clients;
using ContentService.Sdk.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;

namespace ContentService.Components.Components;

/// <summary>
/// Google Photos-inspired embeddable image gallery, recursive album browser, and picker powered solely by <c>ALKsPersonalUI</c>.
/// </summary>
/// <remarks>
/// Designed as a drop-in Blazor component for host services (such as <c>EditorService</c>) so they do not have to build image upload,
/// search, album navigation, or metadata editing UI themselves.
/// Register <c>AddContentServiceSdk(...)</c> and <c>AddPersonalUILibrary(...)</c> in the host's <c>Program.cs</c>, then render
/// <c>&lt;ContentImageGallery OnImageSelected="..." DefaultAlbumPath="Madaffald" LockToDefaultAlbum="true" /&gt;</c>.
/// </remarks>
public sealed partial class ContentImageGallery : ComponentBase, IDisposable
{
        private const int FIRST_PAGE = 1;
        private const int DEFAULT_GALLERY_PAGE_SIZE = 24;
        private const int SEARCH_DEBOUNCE_MILLISECONDS = 250;
        private const string GRID_MIN_COLUMN_WIDTH = "220px";

        private readonly List<ContentImageResponse> _images = [];
        private readonly GalleryLoadCoordinator _loadCoordinator = new();
        private IReadOnlyList<string> _childAlbumPaths = [];
        private string _searchTerm = string.Empty;
        private string? _currentAlbumPath;
        private string? _appliedDefaultAlbumPath;
        private bool _hasInitialized;
        private bool _includeSubalbums = true;
        private int _currentPage = FIRST_PAGE;
        private int _totalPages;
        private int _totalCount;
        private bool _isLoading;
        private bool _isDeleting;
        private bool _isSavingMetadata;
        private bool _isUploadModalOpen;
        private bool _isDetailModalOpen;
        private Guid? _selectedImageId;
        private ContentImageResponse? _inspectedImage;

        [Inject]
        private IContentServiceImageClient ImageClient { get; set; } = default!;

        [Inject]
        private ToastService Toasts { get; set; } = default!;

        /// <summary>
        /// Gets or sets the callback invoked when a host service (such as EditorService) selects an image.
        /// </summary>
        /// <remarks>
        /// Setting this parameter automatically turns on selection checkboxes on each gallery tile and displays the primary
        /// select action button inside <see cref="ContentImageDetailModal"/>. Leave unset for browse/manage-only mode.
        /// </remarks>
        [Parameter]
        public EventCallback<ContentImageResponse> OnImageSelected { get; set; }

        /// <summary>
        /// Gets or sets the controlled selected image identifier used by picker modals with explicit confirmation.
        /// </summary>
        [Parameter]
        public Guid? SelectedImageId { get; set; }

        /// <summary>
        /// Gets or sets the callback invoked when pending selection changes (including deselection) without immediately confirming.
        /// </summary>
        [Parameter]
        public EventCallback<ContentImageResponse?> OnSelectionChanged { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether hold-to-confirm soft deletion is enabled inside the detail modal.
        /// </summary>
        /// <remarks>
        /// Soft-deleted images are hidden from the gallery, but ContentService still blocks deletion with HTTP 409 if any template references the image.
        /// </remarks>
        [Parameter]
        public bool AllowDelete { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether users can edit description, tags, and album path from the detail modal.
        /// </summary>
        [Parameter]
        public bool AllowEditMetadata { get; set; } = true;

        /// <summary>
        /// Gets or sets the optional default recursive album path (e.g., <c>Madaffald</c>) used by host apps to file uploads automatically.
        /// </summary>
        [Parameter]
        public string? DefaultAlbumPath { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether navigation and uploads stay scoped under <see cref="DefaultAlbumPath"/>.
        /// </summary>
        /// <remarks>
        /// When <see langword="true"/>, the global "All Images" root breadcrumb is hidden and users can only browse or upload inside
        /// <see cref="DefaultAlbumPath"/> and its nested sub-albums.
        /// </remarks>
        [Parameter]
        public bool LockToDefaultAlbum { get; set; }

        /// <summary>
        /// Gets or sets the number of images requested per gallery page (minimum 1; clamped in <see cref="LoadPageAsync"/>).
        /// </summary>
        [Parameter]
        public int PageSize { get; set; } = DEFAULT_GALLERY_PAGE_SIZE;

        /// <inheritdoc />
        protected override Task OnInitializedAsync()
        {
                // Mark initialization before normalizing so OnParametersSetAsync does not fire a duplicate initial load.
                this._hasInitialized = true;
                this._appliedDefaultAlbumPath = AlbumPathNormalizer.Normalize(rawAlbumPath: this.DefaultAlbumPath);
                this._currentAlbumPath = this._appliedDefaultAlbumPath;
                return this.LoadCurrentSelectionAsync(page: FIRST_PAGE);
        }

        /// <inheritdoc />
        protected override Task OnParametersSetAsync()
        {
                if (this.OnSelectionChanged.HasDelegate)
                {
                        this._selectedImageId = this.SelectedImageId;
                }

                // Blazor calls OnParametersSetAsync right after OnInitializedAsync; skip until the first render has initialized state.
                if (!this._hasInitialized)
                {
                        return Task.CompletedTask;
                }

                // Only reset the active folder and reload if the host actually changed DefaultAlbumPath (e.g., switching game context buttons).
                string? normalizedDefault = AlbumPathNormalizer.Normalize(rawAlbumPath: this.DefaultAlbumPath);
                if (!string.Equals(a: normalizedDefault, b: this._appliedDefaultAlbumPath, comparisonType: StringComparison.OrdinalIgnoreCase))
                {
                        this._appliedDefaultAlbumPath = normalizedDefault;
                        this._currentAlbumPath = normalizedDefault;
                        return this.LoadCurrentSelectionAsync(page: FIRST_PAGE);
                }

                return Task.CompletedTask;
        }

        /// <summary>
        /// Cancels and disposes any active catalog query or search debounce timer when the component unmounts.
        /// </summary>
        public void Dispose()
        {
                this._loadCoordinator.Dispose();
        }

        /// <summary>
        /// Cancels any previous catalog request and starts loading <paramref name="page"/> with a fresh cancellation token.
        /// </summary>
        private Task LoadCurrentSelectionAsync(int page)
        {
                CancellationToken token = this._loadCoordinator.BeginNewRequestToken();
                return this.LoadPageAsync(page: page, cancellationToken: token);
        }

        /// <summary>
        /// Loads the requested image page and its child albums in parallel while guarding against stale responses overwriting newer UI state.
        /// </summary>
        private async Task LoadPageAsync(
                int page,
                CancellationToken cancellationToken)
        {
                // Bail out early if the component unmounted or a newer user action already replaced this token before execution reached here.
                if (this._loadCoordinator.IsDisposed || cancellationToken.IsCancellationRequested)
                {
                        return;
                }

                this._isLoading = true;

                try
                {
                        ContentImageQuery query = new()
                        {
                                Page = Math.Max(val1: FIRST_PAGE, val2: page),
                                PageSize = Math.Max(val1: FIRST_PAGE, val2: this.PageSize),
                                SearchTerm = this._searchTerm,
                                AlbumPath = this._currentAlbumPath,
                                IncludeSubalbums = this._includeSubalbums
                        };

                        (ContentPagedResponse<ContentImageResponse> result, IReadOnlyList<string> albums) =
                                await GalleryLoadCoordinator.FetchCatalogAsync(
                                        imageClient: this.ImageClient,
                                        query: query,
                                        cancellationToken: cancellationToken);

                        // Double-check after awaiting: if a newer search/navigation canceled our token right as the HTTP calls finished, discard stale data.
                        if (this._loadCoordinator.IsDisposed || cancellationToken.IsCancellationRequested)
                        {
                                return;
                        }

                        this._images.Clear();
                        this._images.AddRange(collection: result.Items);
                        this._childAlbumPaths = albums;
                        this._currentPage = result.Page;
                        this._totalCount = result.TotalCount;
                        this._totalPages = result.TotalPages;
                }
                catch (OperationCanceledException)
                {
                        // Expected whenever the user types quickly, switches albums, or leaves the page; the newer request owns the UI now.
                }
                catch (Exception ex) when (GalleryLoadCoordinator.IsRecoverableClientException(exception: ex))
                {
                        this.ReportClientError(exception: ex, operationPrefix: "Unable to load images");
                }
                finally
                {
                        // Only turn off the spinner if WE are still the active request; otherwise the newer request still needs _isLoading = true.
                        if (!this._loadCoordinator.IsDisposed && !cancellationToken.IsCancellationRequested)
                        {
                                this._isLoading = false;
                        }
                }
        }

        /// <summary>
        /// Debounces search box input for 250 ms and cancels any previous timer or HTTP request before querying ContentService.
        /// </summary>
        private async Task HandleSearchChangedAsync(string value)
        {
                string normalized = value ?? string.Empty;
                if (string.Equals(a: normalized, b: this._searchTerm, comparisonType: StringComparison.Ordinal))
                {
                        return;
                }

                this._searchTerm = normalized;

                // Cancel any in-flight load immediately and keep _isLoading false while waiting for the user to pause typing.
                CancellationToken debounceToken = this._loadCoordinator.BeginNewRequestToken();
                this._isLoading = false;

                try
                {
                        // Debounce keystrokes: if the user types another character within 250 ms, BeginNewRequestToken cancels Task.Delay here.
                        await Task.Delay(millisecondsDelay: SEARCH_DEBOUNCE_MILLISECONDS, cancellationToken: debounceToken);
                        await this.LoadPageAsync(page: FIRST_PAGE, cancellationToken: debounceToken);
                }
                catch (OperationCanceledException)
                {
                        // Expected: another keystroke, album click, or Dispose() intentionally canceled this pending debounce timer.
                }
        }

        /// <summary>
        /// Navigates into a selected album or breadcrumb while enforcing <see cref="LockToDefaultAlbum"/> boundaries.
        /// </summary>
        private Task HandleAlbumNavigatedAsync(string? targetAlbumPath)
        {
                string? normalizedTarget = AlbumPathNormalizer.Normalize(rawAlbumPath: targetAlbumPath);

                // Prevent escaping above DefaultAlbumPath (e.g., clicking root or an ancestor path) when the host locks gallery scope.
                if (this.LockToDefaultAlbum
                        && !ContentGalleryLayout.IsWithinLockedScope(
                                candidateAlbumPath: normalizedTarget,
                                lockedRootAlbumPath: this._appliedDefaultAlbumPath))
                {
                        normalizedTarget = this._appliedDefaultAlbumPath;
                }

                this._currentAlbumPath = normalizedTarget;
                return this.LoadCurrentSelectionAsync(page: FIRST_PAGE);
        }

        private Task HandleIncludeSubalbumsChangedAsync(bool includeSubalbums)
        {
                this._includeSubalbums = includeSubalbums;
                return this.LoadCurrentSelectionAsync(page: FIRST_PAGE);
        }

        private Task HandleRefreshClickedAsync() =>
                this.LoadCurrentSelectionAsync(page: this._currentPage);

        private Task HandlePreviousPageClickedAsync() =>
                this._currentPage > FIRST_PAGE
                        ? this.LoadCurrentSelectionAsync(page: this._currentPage - 1)
                        : Task.CompletedTask;

        private Task HandleNextPageClickedAsync() =>
                this._currentPage < this._totalPages
                        ? this.LoadCurrentSelectionAsync(page: this._currentPage + 1)
                        : Task.CompletedTask;

        private void HandleOpenUploadModal() => this._isUploadModalOpen = true;

        private void HandleUploadModalVisibilityChanged(bool isOpen) => this._isUploadModalOpen = isOpen;

        private async Task HandleUploadCompletedAsync(IReadOnlyList<ContentImageResponse> uploaded)
        {
                if (uploaded.Count > 0)
                {
                        this._selectedImageId = uploaded[0].Id;
                }

                await this.LoadCurrentSelectionAsync(page: FIRST_PAGE);
        }

        private bool IsSelectionEnabled() =>
                this.OnImageSelected.HasDelegate || this.OnSelectionChanged.HasDelegate;

        private async Task HandleTileClickedAsync(ContentImageResponse image)
        {
                // In deferred-selection picker mode, clicking the preview toggles selection just like the blue dot.
                if (this.OnSelectionChanged.HasDelegate)
                {
                        bool isSelected = this._selectedImageId != image.Id;
                        await this.HandleTileSelectionToggledAsync(image: image, isSelected: isSelected);
                        return;
                }

                this._inspectedImage = image;
                this._isDetailModalOpen = true;
        }

        private async Task HandleTileSelectionToggledAsync(
                ContentImageResponse image,
                bool isSelected)
        {
                if (!this.IsSelectionEnabled())
                {
                        return;
                }

                this._selectedImageId = isSelected ? image.Id : null;

                if (this.OnSelectionChanged.HasDelegate)
                {
                        await this.OnSelectionChanged.InvokeAsync(arg: isSelected ? image : null);
                }

                if (isSelected && this.OnImageSelected.HasDelegate)
                {
                        await this.OnImageSelected.InvokeAsync(arg: image);
                }
        }

        private void HandleDetailModalVisibilityChanged(bool isOpen)
        {
                this._isDetailModalOpen = isOpen;
                if (!isOpen)
                {
                        this._inspectedImage = null;
                }
        }

        private async Task HandleDetailSelectRequestedAsync(ContentImageResponse image)
        {
                this._selectedImageId = image.Id;
                this._isDetailModalOpen = false;
                await this.OnImageSelected.InvokeAsync(arg: image);
        }

        /// <summary>
        /// Sends a metadata PATCH using the inspected image's optimistic concurrency stamp and reloads the page on success or conflict.
        /// </summary>
        private async Task HandleDetailSaveMetadataRequestedAsync(ContentUpdateImageMetadataRequest request)
        {
                // Guard against double-clicks while the PATCH request is still in flight.
                if (this._inspectedImage is null || this._isSavingMetadata)
                {
                        return;
                }

                this._isSavingMetadata = true;
                try
                {
                        ContentImageResponse updated = await this.ImageClient.UpdateImageMetadataAsync(
                                imageId: this._inspectedImage.Id,
                                command: request);

                        // Updating _inspectedImage rotates its ConcurrencyStamp so the child detail modal exits edit mode cleanly.
                        this._inspectedImage = updated;
                        this.Toasts.ShowSuccess(messageText: $"Updated '{updated.FileName}'.");
                        await this.LoadCurrentSelectionAsync(page: this._currentPage);
                }
                catch (Exception ex) when (GalleryLoadCoordinator.IsRecoverableClientException(exception: ex))
                {
                        this.ReportClientError(exception: ex, operationPrefix: "Update failed");

                        // Reload the current page even on failure (especially HTTP 409 conflict) so the user gets the newest ConcurrencyStamp.
                        await this.LoadCurrentSelectionAsync(page: this._currentPage);
                }
                finally
                {
                        this._isSavingMetadata = false;
                }
        }

        /// <summary>
        /// Soft-deletes the inspected image and adjusts the active page index if the last tile on a later page was removed.
        /// </summary>
        private async Task HandleDetailDeleteRequestedAsync(ContentImageResponse image)
        {
                if (!this.AllowDelete || this._isDeleting)
                {
                        return;
                }

                this._isDeleting = true;
                try
                {
                        await this.ImageClient.DeleteImageAsync(imageId: image.Id);
                        this._isDetailModalOpen = false;
                        this._inspectedImage = null;

                        if (this._selectedImageId == image.Id)
                        {
                                this._selectedImageId = null;
                        }

                        this.Toasts.ShowSuccess(messageText: $"Deleted '{image.FileName}'.");

                        // Step back one page if the user deleted the final item on a page past page 1 so they do not land on an empty page.
                        int targetPage = this._images.Count <= 1 && this._currentPage > FIRST_PAGE
                                ? this._currentPage - 1
                                : this._currentPage;

                        await this.LoadCurrentSelectionAsync(page: targetPage);
                }
                catch (Exception ex) when (GalleryLoadCoordinator.IsRecoverableClientException(exception: ex))
                {
                        this.ReportClientError(exception: ex, operationPrefix: "Delete failed");
                }
                finally
                {
                        this._isDeleting = false;
                }
        }

        private string? ResolveLockedRootAlbumPath() =>
                this.LockToDefaultAlbum ? this._appliedDefaultAlbumPath : null;

        private void ReportClientError(
                Exception exception,
                string operationPrefix)
        {
                if (this._loadCoordinator.IsDisposed)
                {
                        return;
                }

                this.Toasts.ShowDanger(messageText: GalleryLoadCoordinator.FormatClientError(
                        exception: exception,
                        operationPrefix: operationPrefix));
        }

        private string ResolveImagePreviewSource(Guid imageId)
        {
                try
                {
                        // Load the saved lightweight WebP preview for gallery tiles so multi-megabyte originals do not stall the grid.
                        return this.ImageClient.GetImagePreviewUri(imageId: imageId).AbsoluteUri;
                }
                catch (Exception ex) when (ex is InvalidOperationException or OptionsValidationException)
                {
                        return string.Empty;
                }
        }

        private string ResolveImageContentSource(Guid imageId)
        {
                try
                {
                        return this.ImageClient.GetImageContentUri(imageId: imageId).AbsoluteUri;
                }
                catch (Exception ex) when (ex is InvalidOperationException or OptionsValidationException)
                {
                        // Guard against misconfigured BaseAddress so tile markup renders broken/empty thumbnails instead of crashing the whole Blazor tree.
                        return string.Empty;
                }
        }

        private string ResolveInspectedImageSource() =>
                this._inspectedImage is null
                        ? string.Empty
                        : this.ResolveImageContentSource(imageId: this._inspectedImage.Id);

        private string ResolveImageAlt(ContentImageResponse image) =>
                string.IsNullOrWhiteSpace(value: image.AltText) ? image.FileName : image.AltText;
}
