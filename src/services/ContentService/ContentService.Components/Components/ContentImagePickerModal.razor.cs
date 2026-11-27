using ALKsPersonalUI.Core;
using ContentService.Sdk.Models;
using Microsoft.AspNetCore.Components;

namespace ContentService.Components.Components;

/// <summary>
/// Ready-to-use modal window wrapping <see cref="ContentImageGallery"/> for selecting an image from ContentService.
/// </summary>
public sealed partial class ContentImagePickerModal : ComponentBase
{
        private const string DEFAULT_TITLE = "Vælg billede";
        private const int DEFAULT_PAGE_SIZE = 24;
        private const string BODY_MAX_HEIGHT = "70vh";

        private ContentImageMetadata? _pendingSelectedImage;
        private bool _wasOpen;

        /// <summary>
        /// Gets or sets a value indicating whether the image picker modal is open.
        /// </summary>
        [Parameter]
        public bool IsOpen { get; set; }

        /// <summary>
        /// Gets or sets the two-way binding callback for <see cref="IsOpen"/>.
        /// </summary>
        [Parameter]
        public EventCallback<bool> IsOpenChanged { get; set; }

        /// <summary>
        /// Gets or sets the modal header title.
        /// </summary>
        [Parameter]
        public string Title { get; set; } = DEFAULT_TITLE;

        /// <summary>
        /// Gets or sets the callback invoked when the user selects an image.
        /// </summary>
        [Parameter]
        public EventCallback<ContentImageMetadata> OnImageSelected { get; set; }

        /// <summary>
        /// Gets or sets the optional default recursive album path opened by the embedded gallery.
        /// </summary>
        [Parameter]
        public string? DefaultAlbumPath { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether navigation and uploads stay scoped under <see cref="DefaultAlbumPath"/>.
        /// </summary>
        [Parameter]
        public bool LockToDefaultAlbum { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether metadata editing is enabled inside the picker's detail modal.
        /// </summary>
        [Parameter]
        public bool AllowEditMetadata { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether soft deletion is enabled inside the picker's detail modal.
        /// </summary>
        [Parameter]
        public bool AllowDelete { get; set; }

        /// <summary>
        /// Gets or sets the number of images requested per page.
        /// </summary>
        [Parameter]
        public int PageSize { get; set; } = DEFAULT_PAGE_SIZE;

        /// <summary>
        /// Gets or sets a value indicating whether the modal closes automatically after an image is selected.
        /// </summary>
        [Parameter]
        public bool CloseOnSelect { get; set; } = true;

        /// <inheritdoc />
        protected override void OnParametersSet()
        {
                // Clear any stale pending selection whenever the modal closes or freshly opens.
                if (!this.IsOpen)
                {
                        this._wasOpen = false;
                        this._pendingSelectedImage = null;
                        return;
                }

                if (!this._wasOpen)
                {
                        this._wasOpen = true;
                        this._pendingSelectedImage = null;
                }
        }

        private void HandlePendingSelectionChanged(ContentImageMetadata? image) =>
                this._pendingSelectedImage = image;

        private async Task HandleConfirmSelectionAsync()
        {
                if (this._pendingSelectedImage is null)
                {
                        return;
                }

                await this.OnImageSelected.InvokeAsync(arg: this._pendingSelectedImage);
                if (this.CloseOnSelect)
                {
                        await this.IsOpenChanged.InvokeAsync(arg: false);
                }
        }

        private Task HandleCloseAsync() => this.IsOpenChanged.InvokeAsync(arg: false);

        private UIModifier BuildModalModifier() =>
                UIModifier.New
                        .FillMaxWidth();

        private UIModifier BuildBodyModifier() =>
                UIModifier.New
                        .FillMaxWidth()
                        .MaxHeight(BODY_MAX_HEIGHT)
                        .VerticalScroll();
}

