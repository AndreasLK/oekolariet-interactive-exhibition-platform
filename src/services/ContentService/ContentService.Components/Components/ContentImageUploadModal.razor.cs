using ALKsPersonalUI.Core;
using ALKsPersonalUI.Inputs;
using ALKsPersonalUI.Overlays;
using ContentService.Components.Internal;
using ContentService.Sdk.Clients;
using ContentService.Sdk.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace ContentService.Components.Components;

/// <summary>
/// Controlled modal component supporting single-file and recursive folder uploads with bounded previews, existing library suggestions, and searchable metadata.
/// </summary>
/// <remarks>
/// Designed for Blazor WebAssembly: overrides Blazor's 512 KiB <c>OpenReadStream</c> cap to 10 MiB, bounds folder batches to 50 files,
/// and caps thumbnail previews so large exhibition folders never exhaust browser WASM memory.
/// </remarks>
public sealed partial class ContentImageUploadModal : ComponentBase, IAsyncDisposable
{
        private const long MAX_IMAGE_SIZE_BYTES = ContentImageBatchUploader.MAX_IMAGE_SIZE_BYTES;
        private const int MAX_FILES_PER_BATCH = ContentImageBatchUploader.MAX_FILES_PER_BATCH;
        private const int MAX_PREVIEW_ITEMS = 12;
        private const long MAX_PREVIEW_BYTES_TOTAL = 8L * 1024L * 1024L;
        private const int MAX_ALT_TEXT_LENGTH = 500;
        private const int ALT_TEXT_ROWS = 3;
        private const int MAX_TAGS_LENGTH = 500;
        private const int MAX_ALBUM_PATH_LENGTH = 256;
        private const int SINGLE_LINE_MAX_LINES = 1;
        private const int SUGGESTIONS_PAGE = 1;
        private const int SUGGESTIONS_PAGE_SIZE = 100;
        private const string ACCEPTED_IMAGE_EXTENSIONS = ".png,.jpg,.jpeg,.webp";
        private const string FOLDER_PICKER_JS_PATH = "./_content/ContentService.Components/js/folderPicker.js";

        private readonly List<SelectedImageFile> _pendingFiles = [];
        private readonly List<string> _suggestedAlbums = [];
        private readonly List<string> _suggestedTags = [];
        private ElementReference _hiddenFolderUploadContainer;
        private IJSObjectReference? _folderPickerModule;
        private string _altText = string.Empty;
        private string _tags = string.Empty;
        private string _albumPath = string.Empty;
        private int _uploadInputKey;
        private bool _wasOpen;
        private bool _isUploading;
        private bool _isFolderSelection;

        [Inject]
        private IContentServiceImageClient ImageClient { get; set; } = default!;

        [Inject]
        private ToastService Toasts { get; set; } = default!;

        [Inject]
        private IJSRuntime JsRuntime { get; set; } = default!;

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
        public EventCallback<IReadOnlyList<ContentImageResponse>> OnUploadCompleted { get; set; }

        /// <inheritdoc />
        protected override async Task OnParametersSetAsync()
        {
                // Reset the form whenever the parent closes the modal, unless an upload batch is still actively streaming.
                if (!this.IsOpen && !this._isUploading)
                {
                        this._wasOpen = false;
                        this.ResetFormState();
                        return;
                }

                // Prefill album path and load library suggestions ONLY on the closed->open transition (_wasOpen == false).
                if (this.IsOpen && !this._wasOpen)
                {
                        this._wasOpen = true;
                        this._albumPath = AlbumPathNormalizer.Normalize(rawAlbumPath: this.DefaultAlbumPath) ?? string.Empty;
                        await this.LoadLibrarySuggestionsAsync();
                }
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
                if (this._folderPickerModule is null)
                {
                        return;
                }

                try
                {
                        await this._folderPickerModule.DisposeAsync();
                }
                catch (JSDisconnectedException)
                {
                        // Ignore circuit disconnect during teardown.
                }
        }

        /// <summary>
        /// Loads existing album paths and tags from the library so users can click to reuse them without typos.
        /// </summary>
        private async Task LoadLibrarySuggestionsAsync()
        {
                this._suggestedAlbums.Clear();
                this._suggestedTags.Clear();

                try
                {
                        Task<IReadOnlyList<string>> albumsTask = this.ImageClient.ListAlbumsAsync(parentPath: null);
                        Task<ContentPagedResponse<ContentImageResponse>> imagesTask = this.ImageClient.ListImagesAsync(
                                query: new ContentImageQuery
                                {
                                        Page = SUGGESTIONS_PAGE,
                                        PageSize = SUGGESTIONS_PAGE_SIZE,
                                        IncludeSubalbums = true
                                });

                        await Task.WhenAll(albumsTask, imagesTask);

                        this.CollectSuggestedAlbums(rootAlbums: await albumsTask, images: (await imagesTask).Items);
                        this.CollectSuggestedTags(images: (await imagesTask).Items);
                }
                catch (Exception)
                {
                        // Non-fatal: failing to fetch optional autocomplete pills should never block image uploads.
                }
        }

        private void CollectSuggestedAlbums(
                IReadOnlyList<string> rootAlbums,
                IReadOnlyList<ContentImageResponse> images)
        {
                HashSet<string> uniqueAlbums = new(comparer: StringComparer.OrdinalIgnoreCase);

                foreach (string album in rootAlbums)
                {
                        if (!string.IsNullOrWhiteSpace(value: album))
                        {
                                uniqueAlbums.Add(item: album.Trim());
                        }
                }

                foreach (ContentImageResponse image in images)
                {
                        if (!string.IsNullOrWhiteSpace(value: image.AlbumPath))
                        {
                                uniqueAlbums.Add(item: image.AlbumPath.Trim());
                        }
                }

                this._suggestedAlbums.AddRange(collection: uniqueAlbums.OrderBy(keySelector: static album => album, comparer: StringComparer.OrdinalIgnoreCase));
        }

        private void CollectSuggestedTags(IReadOnlyList<ContentImageResponse> images)
        {
                HashSet<string> uniqueTags = new(comparer: StringComparer.OrdinalIgnoreCase);

                foreach (ContentImageResponse image in images)
                {
                        foreach (string tag in image.Tags)
                        {
                                if (!string.IsNullOrWhiteSpace(value: tag))
                                {
                                        uniqueTags.Add(item: tag.Trim());
                                }
                        }
                }

                this._suggestedTags.AddRange(collection: uniqueTags.OrderBy(keySelector: static tag => tag, comparer: StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Triggers the hidden webkitdirectory input directly via minimal JS so the Mappe button opens the OS folder dialog in one click.
        /// </summary>
        private async Task HandleOpenFolderPickerAsync()
        {
                if (this._isUploading)
                {
                        return;
                }

                try
                {
                        this._folderPickerModule ??= await this.JsRuntime.InvokeAsync<IJSObjectReference>(
                                identifier: "import",
                                FOLDER_PICKER_JS_PATH);

                        await this._folderPickerModule.InvokeVoidAsync(
                                identifier: "openFolderPicker",
                                this._hiddenFolderUploadContainer);
                }
                catch (JSException ex)
                {
                        this.Toasts.ShowDanger(messageText: $"Kunne ikke åbne mappevælgeren: {ex.Message}");
                }
                catch (JSDisconnectedException)
                {
                        // Ignore circuit disconnect when navigating away mid-click.
                }
        }

        /// <summary>
        /// Captures files selected from the primary dropzone while enforcing the WASM batch cap and generating bounded previews.
        /// </summary>
        private Task HandleFileImagesSelectedAsync(IReadOnlyList<IBrowserFile> selectedFiles) =>
                this.ApplySelectedFilesAsync(selectedFiles: selectedFiles, isFolderSelection: false);

        /// <summary>
        /// Captures files selected from the hidden folder picker while enforcing the WASM batch cap and generating bounded previews.
        /// </summary>
        private Task HandleFolderImagesSelectedAsync(IReadOnlyList<IBrowserFile> selectedFiles) =>
                this.ApplySelectedFilesAsync(selectedFiles: selectedFiles, isFolderSelection: true);

        private async Task ApplySelectedFilesAsync(
                IReadOnlyList<IBrowserFile> selectedFiles,
                bool isFolderSelection)
        {
                this._pendingFiles.Clear();
                this._isFolderSelection = isFolderSelection && selectedFiles.Count > 0;

                if (selectedFiles.Count > MAX_FILES_PER_BATCH)
                {
                        this.Toasts.ShowWarning(messageText: $"Upload er begrænset til {MAX_FILES_PER_BATCH} filer pr. gang; overskydende filer blev sprunget over.");
                }

                long remainingPreviewBytes = MAX_PREVIEW_BYTES_TOTAL;
                int previewCount = 0;

                foreach (IBrowserFile selectedFile in selectedFiles.Take(count: MAX_FILES_PER_BATCH))
                {
                        string? previewDataUrl = null;
                        bool canGeneratePreview = previewCount < MAX_PREVIEW_ITEMS && selectedFile.Size <= remainingPreviewBytes;

                        if (canGeneratePreview)
                        {
                                previewDataUrl = await TryCreatePreviewDataUrlAsync(file: selectedFile);
                                if (previewDataUrl is not null)
                                {
                                        previewCount++;
                                        remainingPreviewBytes -= selectedFile.Size;
                                }
                        }

                        this._pendingFiles.Add(item: new SelectedImageFile(
                                File: selectedFile,
                                PreviewDataUrl: previewDataUrl));
                }
        }

        private static async Task<string?> TryCreatePreviewDataUrlAsync(IBrowserFile file)
        {
                try
                {
                        await using Stream stream = file.OpenReadStream(maxAllowedSize: MAX_IMAGE_SIZE_BYTES);
                        using MemoryStream buffer = new();
                        await stream.CopyToAsync(destination: buffer);

                        string contentType = ImageContentTypeNormalizer.Normalize(
                                rawContentType: file.ContentType,
                                fileName: file.Name);
                        string base64 = Convert.ToBase64String(inArray: buffer.ToArray());
                        return $"data:{contentType};base64,{base64}";
                }
                catch (Exception)
                {
                        // Non-fatal: preview failures should never block queuing valid files for upload.
                        return null;
                }
        }

        private void HandleSuggestedAlbumClicked(string albumPath)
        {
                bool isAlreadySelected = string.Equals(a: this._albumPath.Trim(), b: albumPath, comparisonType: StringComparison.OrdinalIgnoreCase);
                this._albumPath = isAlreadySelected ? string.Empty : albumPath;
        }

        private bool IsTagSelected(string tag) =>
                this.ParseCurrentTags().Any(predicate: existing => string.Equals(a: existing, b: tag, comparisonType: StringComparison.OrdinalIgnoreCase));

        private void HandleSuggestedTagClicked(string tag)
        {
                List<string> currentTags = this.ParseCurrentTags();
                int existingIndex = currentTags.FindIndex(match: existing => string.Equals(a: existing, b: tag, comparisonType: StringComparison.OrdinalIgnoreCase));

                if (existingIndex >= 0)
                {
                        currentTags.RemoveAt(index: existingIndex);
                }
                else
                {
                        currentTags.Add(item: tag);
                }

                this._tags = string.Join(separator: ", ", values: currentTags);
        }

        private List<string> ParseCurrentTags() =>
                this._tags
                        .Split(separator: ',', options: StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Where(predicate: static tag => !string.IsNullOrWhiteSpace(value: tag))
                        .ToList();

        private void HandleRemovePendingFile(SelectedImageFile selectedFile)
        {
                this._pendingFiles.Remove(item: selectedFile);

                if (this._pendingFiles.Count == 0)
                {
                        this._isFolderSelection = false;

                        // Reset the underlying ImageUpload inputs once the last pending file is removed.
                        this._uploadInputKey++;
                }
        }

        private void HandleClearPendingFiles()
        {
                this._pendingFiles.Clear();
                this._isFolderSelection = false;

                // Increment the @key so both ImageUpload inputs reset their internal file state cleanly.
                this._uploadInputKey++;
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
                (List<ContentImageResponse> Uploaded, int FailedCount) outcome;

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
                IReadOnlyList<ContentImageResponse> uploaded,
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
                        this.Toasts.ShowSuccess(messageText: $"Uploadede {uploaded.Count} billede(r).");
                }
                else
                {
                        this.Toasts.ShowWarning(messageText: $"Uploadede {uploaded.Count} billede(r); {failedCount} fejlede.");
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
                this._isFolderSelection = false;
                this._altText = string.Empty;
                this._tags = string.Empty;
                this._albumPath = string.Empty;
                this._uploadInputKey++;
        }

        private static string FormatSelectionSummary(int selectedCount) =>
                selectedCount == 1
                        ? "1 billede valgt"
                        : $"{selectedCount} billeder valgt";

        private string ResolveUploadButtonText() =>
                this._isUploading ? "Uploader..." : "Upload";

        private UIModifier BuildModalModifier() =>
                UIModifier.New.FillMaxWidth();

        private UIModifier BuildPrimaryDropzoneModifier() =>
                UIModifier.New.Weight(flexWeight: 1).MinWidth(minWidth: "0");

        private UIModifier BuildFolderButtonModifier() =>
                // Use height: auto + align-self: stretch so flex stretch matches the sibling ImageUpload height instead of ActionButton's default fit-content.
                UIModifier.New.Width(width: "4.5rem").Height(height: "auto").AlignSelf(alignment: VerticalAlignment.Stretch);

        private UIModifier BuildPreviewCardModifier() =>
                UIModifier.New
                        .Width(width: "10.5rem")
                        .Padding(padding: "0.5rem")
                        .Border(thickness: "1px", borderColor: "var(--ui-border, rgba(255, 255, 255, 0.14))")
                        .BorderRadius(borderRadius: "var(--ui-radius-md, 0.75rem)")
                        .Background(backgroundColor: "var(--ui-bg-surface, #1c1c28)");

        private UIModifier BuildPreviewImageModifier() =>
                UIModifier.New
                        .FillMaxWidth()
                        .Height(height: "5.5rem")
                        .BorderRadius(borderRadius: "var(--ui-radius-sm, 0.5rem)");
}
