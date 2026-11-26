using ALKsPersonalUI.Core;
using ALKsPersonalUI.Inputs;
using ALKsPersonalUI.Overlays;
using ContentService.Components.Internal;
using ContentService.Sdk.Clients;
using ContentService.Sdk.Models;
using Microsoft.AspNetCore.Components;

namespace ContentService.Components.Components;

/// <summary>
/// Controlled modal component supporting single-file and recursive folder uploads with bounded previews and searchable metadata.
/// </summary>
/// <remarks>
/// Designed for Blazor WebAssembly: overrides Blazor's 512 KiB <c>OpenReadStream</c> cap to 10 MiB, bounds folder batches to 50 files,
/// and caps thumbnail previews so large exhibition folders never exhaust browser WASM memory.
/// </remarks>
public sealed partial class ContentImageUploadModal : ComponentBase
{
        private const long MAX_IMAGE_SIZE_BYTES = ContentImageBatchUploader.MAX_IMAGE_SIZE_BYTES;
        private const int MAX_FILES_PER_BATCH = ContentImageBatchUploader.MAX_FILES_PER_BATCH;
        private const int MAX_PREVIEW_ITEMS = 12;
        private const int MAX_PREVIEW_DIMENSION_PX = 240;
        private const long MAX_PREVIEW_BYTES_TOTAL = 8L * 1024L * 1024L;
        private const int MAX_ALT_TEXT_LENGTH = 500;
        private const int MAX_TAGS_LENGTH = 500;
        private const int MAX_ALBUM_PATH_LENGTH = 256;
        private const int SINGLE_LINE_MAX_LINES = 1;
        private const double MODAL_MAX_WIDTH_PX = 680;
        private const double FILE_ROW_VERTICAL_PADDING_PX = 4;
        private const double FILE_ROW_HORIZONTAL_PADDING_PX = 8;
        private const double FILE_ROW_BORDER_RADIUS_PX = 8;
        private const string ACCEPTED_IMAGE_EXTENSIONS = ".png,.jpg,.jpeg,.webp";

        private readonly List<SelectedImageFile> _pendingFiles = [];
        private string _altText = string.Empty;
        private string _tags = string.Empty;
        private string _albumPath = string.Empty;
        private bool _wasOpen;
        private bool _isUploading;

        [Inject]
        private IContentServiceImageClient ImageClient { get; set; } = default!;

        [Inject]
        private ToastService Toasts { get; set; } = default!;

        /// <summary>
        /// Gets or sets a value indicating whether the upload modal is visible.
        /// </summary>
        [Parameter]
        public bool IsOpen { get; set; }

        /// <summary>
        /// Gets or sets the callback invoked when modal visibility changes.
        /// </summary>
        [Parameter]
        public EventCallback<bool> IsOpenChanged { get; set; }

        /// <summary>
        /// Gets or sets the optional default or active album path prefilled when the modal opens.
        /// </summary>
        /// <remarks>
        /// When uploading a folder, each file's relative subfolder path is appended beneath this base path.
        /// </remarks>
        [Parameter]
        public string? DefaultAlbumPath { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the base album path input is read-only/locked by the host service.
        /// </summary>
        [Parameter]
        public bool LockAlbumPath { get; set; }

        /// <summary>
        /// Gets or sets the callback invoked after at least one image uploads successfully, passing the newly persisted metadata records.
        /// </summary>
        [Parameter]
        public EventCallback<IReadOnlyList<ContentImageMetadata>> OnUploadCompleted { get; set; }

        /// <inheritdoc />
        protected override void OnParametersSet()
        {
                // Reset the form whenever the parent closes the modal, unless an upload batch is still actively streaming.
                if (!this.IsOpen && !this._isUploading)
                {
                        this._wasOpen = false;
                        this.ResetFormState();
                        return;
                }

                // Prefill album path ONLY on the closed->open transition (_wasOpen == false) so parent re-renders never wipe what the user typed mid-session.
                if (this.IsOpen && !this._wasOpen)
                {
                        this._wasOpen = true;
                        this._albumPath = AlbumPathNormalizer.Normalize(rawAlbumPath: this.DefaultAlbumPath) ?? string.Empty;
                }
        }

        /// <summary>
        /// Captures files selected from single/multi-file or folder pickers while enforcing the WASM batch cap.
        /// </summary>
        private void HandleImagesWithPathsSelected(IReadOnlyList<SelectedImageFile> selectedFiles)
        {
                this._pendingFiles.Clear();

                if (selectedFiles.Count > MAX_FILES_PER_BATCH)
                {
                        this.Toasts.ShowWarning(messageText: $"Batch capped at {MAX_FILES_PER_BATCH} files; extra files were skipped.");
                }

                foreach (SelectedImageFile selectedFile in selectedFiles.Take(count: MAX_FILES_PER_BATCH))
                {
                        this._pendingFiles.Add(item: selectedFile);
                }
        }

        private void HandleAlbumPathChanged(string value) => this._albumPath = value;

        private void HandleAltTextChanged(string value) => this._altText = value;

        private void HandleTagsChanged(string value) => this._tags = value;

        private async Task HandleModalVisibilityChangedAsync(bool isOpen)
        {
                // Ignore backdrop close attempts while multipart streams are actively uploading.
                if (this._isUploading && !isOpen)
                {
                        return;
                }

                if (!isOpen)
                {
                        this._wasOpen = false;
                        this.ResetFormState();
                }

                await this.IsOpenChanged.InvokeAsync(arg: isOpen);
        }

        private Task HandleCancelClickedAsync() =>
                this.HandleModalVisibilityChangedAsync(isOpen: false);

        private async Task HandleUploadClickedAsync()
        {
                if (this._isUploading || this._pendingFiles.Count == 0)
                {
                        return;
                }

                this._isUploading = true;
                (List<ContentImageMetadata> Uploaded, int FailedCount) outcome;

                try
                {
                        // Snapshot the list with ToArray() before awaiting so re-renders cannot mutate _pendingFiles mid-iteration.
                        SelectedImageFile[] batchSnapshot = this._pendingFiles.Take(count: MAX_FILES_PER_BATCH).ToArray();
                        outcome = await ContentImageBatchUploader.UploadBatchAsync(
                                imageClient: this.ImageClient,
                                toasts: this.Toasts,
                                files: batchSnapshot,
                                baseAlbumPath: this._albumPath,
                                altText: this._altText,
                                tags: this._tags);
                }
                finally
                {
                        this._isUploading = false;
                }

                await this.ReportBatchOutcomeAsync(
                        uploaded: outcome.Uploaded,
                        failedCount: outcome.FailedCount);
        }

        private async Task ReportBatchOutcomeAsync(
                IReadOnlyList<ContentImageMetadata> uploaded,
                int failedCount)
        {
                if (uploaded.Count == 0)
                {
                        // Clear failed batch selection so users can pick replacements cleanly without stale errors lingering.
                        this._pendingFiles.Clear();
                        return;
                }

                if (failedCount == 0)
                {
                        this.Toasts.ShowSuccess(messageText: $"Uploaded {uploaded.Count} image(s).");
                }
                else
                {
                        this.Toasts.ShowWarning(messageText: $"Uploaded {uploaded.Count} image(s); {failedCount} failed.");
                }

                this._wasOpen = false;
                this.ResetFormState();
                await this.IsOpenChanged.InvokeAsync(arg: false);
                await this.OnUploadCompleted.InvokeAsync(arg: uploaded);
        }

        private string? ResolveTargetAlbumPath(SelectedImageFile selectedFile) =>
                ContentImageBatchUploader.ResolveTargetAlbumPath(
                        baseAlbumPath: this._albumPath,
                        selectedFile: selectedFile);

        private void ResetFormState()
        {
                this._pendingFiles.Clear();
                this._altText = string.Empty;
                this._tags = string.Empty;
                this._albumPath = string.Empty;
        }

        private string ResolveUploadButtonText() =>
                this._isUploading ? "Uploading..." : "Upload";

        private UIModifier BuildModalModifier() =>
                UIModifier.New.MaxWidth(px: MODAL_MAX_WIDTH_PX).FillMaxWidth();

        private UIModifier BuildPendingFileRowModifier() =>
                UIModifier.New
                        .FillMaxWidth()
                        .Padding(verticalPx: FILE_ROW_VERTICAL_PADDING_PX, horizontalPx: FILE_ROW_HORIZONTAL_PADDING_PX)
                        .BorderRadius(px: FILE_ROW_BORDER_RADIUS_PX);
}
