using ALKsPersonalUI.Core;
using ContentService.Sdk.Models;
using Microsoft.AspNetCore.Components;

namespace ContentService.Components.Components;

/// <summary>
/// Controlled lightbox modal rendering a full exhibition image preview, searchable metadata, inline editing, and selection/deletion actions.
/// </summary>
public sealed partial class ContentImageDetailModal : ComponentBase
{
        private const string DEFAULT_TITLE = "Image Details";
        private const double MODAL_MAX_WIDTH_PX = 860;
        private const double PREVIEW_CONTAINER_MAX_HEIGHT_PX = 400;
        private const double PREVIEW_IMAGE_MAX_HEIGHT_PX = 380;
        private const double PREVIEW_BORDER_RADIUS_PX = 12;
        private const int DETAIL_GRID_COLUMNS = 2;
        private const int HOLD_TO_DELETE_DURATION_MS = 1200;
        private const string DELETE_ACCENT_COLOR = "#dc2626";

        private Guid? _activeImageId;
        private Guid? _activeConcurrencyStamp;
        private bool _isEditing;

        /// <summary>
        /// Gets or sets a value indicating whether the modal is currently visible.
        /// </summary>
        [Parameter]
        public bool IsOpen { get; set; }

        /// <summary>
        /// Gets or sets the callback invoked when modal visibility changes.
        /// </summary>
        [Parameter]
        public EventCallback<bool> IsOpenChanged { get; set; }

        /// <summary>
        /// Gets or sets the image metadata displayed inside the modal.
        /// </summary>
        [Parameter]
        public ContentImageMetadata? Image { get; set; }

        /// <summary>
        /// Gets or sets the direct ContentService HTTP image URL used for preview rendering.
        /// </summary>
        [Parameter]
        public string ImageSource { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the primary selection action button is displayed.
        /// </summary>
        [Parameter]
        public bool ShowSelectButton { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether hold-to-confirm deletion is available in the modal footer.
        /// </summary>
        [Parameter]
        public bool AllowDelete { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether inline metadata editing is enabled.
        /// </summary>
        [Parameter]
        public bool AllowEditMetadata { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether a delete operation is currently in flight.
        /// </summary>
        [Parameter]
        public bool IsDeleting { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether metadata persistence is currently in flight.
        /// </summary>
        [Parameter]
        public bool IsSavingMetadata { get; set; }

        /// <summary>
        /// Gets or sets the callback invoked when the user confirms selecting the displayed image.
        /// </summary>
        [Parameter]
        public EventCallback<ContentImageMetadata> OnSelectRequested { get; set; }

        /// <summary>
        /// Gets or sets the callback invoked when the user completes hold-to-confirm deletion.
        /// </summary>
        [Parameter]
        public EventCallback<ContentImageMetadata> OnDeleteRequested { get; set; }

        /// <summary>
        /// Gets or sets the callback invoked when the user saves updated description, tags, or album path.
        /// </summary>
        [Parameter]
        public EventCallback<UpdateImageMetadataRequest> OnSaveMetadataRequested { get; set; }

        /// <inheritdoc />
        protected override void OnParametersSet()
        {
                if (!this.IsOpen)
                {
                        this._isEditing = false;
                        return;
                }

                // Exit edit mode automatically when switching images or when ContentService returns a rotated ConcurrencyStamp after a successful PATCH.
                // Tracking both Id and ConcurrencyStamp guarantees stale edits never persist across concurrent server updates.
                if (this._activeImageId != this.Image?.Id || this._activeConcurrencyStamp != this.Image?.ConcurrencyStamp)
                {
                        this._activeImageId = this.Image?.Id;
                        this._activeConcurrencyStamp = this.Image?.ConcurrencyStamp;
                        this._isEditing = false;
                }
        }

        private string ResolveModalTitle() =>
                this.Image?.FileName ?? DEFAULT_TITLE;

        private string ResolveAltText()
        {
                // Fall back to the file name when no explicit alt text was supplied at upload time.
                return string.IsNullOrWhiteSpace(value: this.Image?.AltText)
                        ? this.Image?.FileName ?? DEFAULT_TITLE
                        : this.Image.AltText;
        }

        private void HandleStartEdit()
        {
                this._isEditing = true;
        }

        private void HandleCancelEdit()
        {
                this._isEditing = false;
        }

        private Task HandleSaveMetadataRequestedAsync(UpdateImageMetadataRequest request) =>
                this.OnSaveMetadataRequested.InvokeAsync(arg: request);

        private Task HandleCloseClickedAsync() =>
                this.IsOpenChanged.InvokeAsync(arg: false);

        private async Task HandleSelectClickedAsync()
        {
                if (this.Image is null)
                {
                        return;
                }

                await this.OnSelectRequested.InvokeAsync(arg: this.Image);
        }

        private async Task HandleDeleteClickedAsync()
        {
                // Guard against duplicate triggers while an async soft-delete request is still awaiting ContentService.
                if (this.Image is null || this.IsDeleting)
                {
                        return;
                }

                await this.OnDeleteRequested.InvokeAsync(arg: this.Image);
        }

        private UIModifier BuildModalModifier() =>
                UIModifier.New.MaxWidth(px: MODAL_MAX_WIDTH_PX).FillMaxWidth();

        private UIModifier BuildPreviewContainerModifier() =>
                UIModifier.New
                        .FillMaxWidth()
                        .MaxHeight(px: PREVIEW_CONTAINER_MAX_HEIGHT_PX)
                        .ClipRounded(px: PREVIEW_BORDER_RADIUS_PX);

        private UIModifier BuildPreviewImageModifier() =>
                UIModifier.New.FillMaxWidth().MaxHeight(px: PREVIEW_IMAGE_MAX_HEIGHT_PX);

        private UIModifier BuildDeleteButtonModifier() =>
                UIModifier.New
                        .AccentColor(accentColor: DELETE_ACCENT_COLOR)
                        .TextColor(textColor: DELETE_ACCENT_COLOR);
}
