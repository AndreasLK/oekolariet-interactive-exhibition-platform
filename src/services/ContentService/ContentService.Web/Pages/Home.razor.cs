using ALKsPersonalUI.Core;
using ALKsPersonalUI.Overlays;
using ContentService.Sdk.Clients;
using ContentService.Sdk.Exceptions;
using ContentService.Sdk.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;

namespace ContentService.Web.Pages;

/// <summary>
/// Standalone test page validating the embeddable <c>ContentImageGallery</c> component, auto-assigned albums, and SDK download round-trip.
/// </summary>
/// <remarks>
/// Acts as the reference integration example for EditorService developers: shows how to switch <c>DefaultAlbumPath</c> /
/// <c>LockToDefaultAlbum</c>, listen to <c>OnImageSelected</c>, and stream-download selected assets via <see cref="IContentServiceImageClient"/>.
/// </remarks>
public sealed partial class Home : ComponentBase, IDisposable
{
        private const int DEFER_BELOW_FOLD_DELAY_MILLISECONDS = 1;

        /// <summary>
        /// Uses an 80 KiB (81,920-byte) stream buffer so allocations stay below .NET's 85,000-byte Large Object Heap (LOH) threshold in Blazor WASM's constrained browser memory.
        /// </summary>
        private const int STREAM_COPY_BUFFER_BYTES = 81920;
        private const double ZERO_PX = 0;
        private const double PAGE_VERTICAL_PADDING_PX = 16;
        private const double PANEL_PADDING_PX = 16;
        private const double PANEL_BORDER_RADIUS_PX = 12;
        private const double PANEL_BORDER_THICKNESS_PX = 1;
        private const double PREVIEW_BOX_MAX_WIDTH_PX = 360;
        private const double PREVIEW_BOX_MAX_HEIGHT_PX = 260;
        private const double PREVIEW_BOX_RADIUS_PX = 8;
        private const double METADATA_COL_MIN_WIDTH_PX = 260;
        private const double METADATA_COL_FLEX_GROW = 1;
        private const string PANEL_BORDER_COLOR_VAR = "var(--pui-border)";
        private const string MADAFFALD_ALBUM = "Madaffald";
        private const string PLASTIK_ALBUM = "Genbrug/Plastik";

        private readonly CancellationTokenSource _disposeCts = new();
        private ContentImageResponse? _selectedImage;
        private ContentTemplateResponse? _selectedTemplate;
        private string? _activeHostAlbum;
        private bool _lockHostAlbum;
        private bool _isTestingDownload;
        private bool _isTemplateGalleryReady;
        private bool _isImagePickerOpen;
        private bool _isTemplatePickerOpen;
        private bool _isDisposed;
        private string? _lastDownloadSummary;

        [Inject]
        private IContentServiceImageClient ImageClient { get; set; } = default!;

        [Inject]
        private ToastService Toasts { get; set; } = default!;

        /// <inheritdoc />
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
                if (!firstRender || this._isTemplateGalleryReady || this._isDisposed)
                {
                        return;
                }

                // Non-standard render yield: A 1 ms Task.Delay lets Blazor WASM flush the first paint and start
                // above-the-fold image requests before ContentTemplateGallery mounts and competes for browser HTTP connections.
                // Loss: Delays template gallery mount by one render frame.
                try
                {
                        await Task.Delay(millisecondsDelay: DEFER_BELOW_FOLD_DELAY_MILLISECONDS, cancellationToken: this._disposeCts.Token);
                }
                catch (OperationCanceledException)
                {
                        return;
                }

                if (this._isDisposed)
                {
                        return;
                }

                this._isTemplateGalleryReady = true;
                this.StateHasChanged();
        }

        /// <inheritdoc />
        public void Dispose()
        {
                if (this._isDisposed)
                {
                        return;
                }

                this._isDisposed = true;
                this._disposeCts.Cancel();
                this._disposeCts.Dispose();
        }

        /// <summary>
        /// Switches the host album scope passed to <c>ContentImageGallery</c> (unrestricted, auto-assigned, or locked subtree).
        /// </summary>
        private void SelectHostContext(
                string? albumPath,
                bool lockAlbum)
        {
                this._activeHostAlbum = albumPath;
                this._lockHostAlbum = lockAlbum;
        }

        /// <summary>
        /// Receives the image picked inside <c>ContentImageGallery</c> or <c>ContentImagePickerModal</c> and resets any stale download verification message.
        /// </summary>
        private Task HandleImageSelectedAsync(ContentImageResponse selectedImage)
        {
                // Clear previous download verification summary whenever a different image is chosen.
                this._selectedImage = selectedImage;
                this._lastDownloadSummary = null;
                this.Toasts.ShowInfo(messageText: $"Selected '{selectedImage.FileName}' ({selectedImage.PictureReference}).");
                return Task.CompletedTask;
        }

        /// <summary>
        /// Receives the template picked inside <c>ContentTemplateGallery</c> or <c>ContentTemplatePickerModal</c>.
        /// </summary>
        private Task HandleTemplateSelectedAsync(ContentTemplateResponse selectedTemplate)
        {
                this._selectedTemplate = selectedTemplate;
                this.Toasts.ShowInfo(messageText: $"Selected template '{selectedTemplate.Name}' ({selectedTemplate.ComponentType}).");
                return Task.CompletedTask;
        }

        private void HandleOpenImagePicker() => this._isImagePickerOpen = true;

        private void HandleImagePickerVisibilityChanged(bool isOpen) => this._isImagePickerOpen = isOpen;

        private void HandleOpenTemplatePicker() => this._isTemplatePickerOpen = true;

        private void HandleTemplatePickerVisibilityChanged(bool isOpen) => this._isTemplatePickerOpen = isOpen;

        /// <summary>
        /// Exercises <see cref="IContentServiceImageClient.DownloadImageAsync(Guid, CancellationToken)"/> end-to-end and disposes the streamed HTTP response safely.
        /// </summary>
        private async Task HandleTestDownloadClickedAsync()
        {
                if (this._selectedImage is null || this._isTestingDownload)
                {
                        return;
                }

                this._isTestingDownload = true;
                try
                {
                        // ContentImageDownloadResponse owns both the network stream and underlying HttpResponseMessage; disposing it closes the connection cleanly.
                        using ContentImageDownloadResponse download = await this.ImageClient.DownloadImageAsync(imageId: this._selectedImage.Id);
                        long downloadedBytes = await CountStreamBytesAsync(stream: download.Content);

                        this._lastDownloadSummary = $"Downloaded {downloadedBytes:N0} bytes ({download.ContentType}).";
                        this.Toasts.ShowSuccess(messageText: this._lastDownloadSummary);
                }
                catch (ContentServiceApiException ex)
                {
                        this._lastDownloadSummary = $"API error ({(int)ex.StatusCode}): {ex.ProblemDetails?.Detail ?? ex.Message}";
                        this.Toasts.ShowDanger(messageText: this._lastDownloadSummary);
                }
                catch (HttpRequestException ex)
                {
                        this._lastDownloadSummary = $"Network error: {ex.Message}";
                        this.Toasts.ShowDanger(messageText: this._lastDownloadSummary);
                }
                catch (Exception ex) when (ex is InvalidOperationException or OptionsValidationException)
                {
                        this._lastDownloadSummary = $"Configuration error: {ex.Message}";
                        this.Toasts.ShowDanger(messageText: this._lastDownloadSummary);
                }
                finally
                {
                        this._isTestingDownload = false;
                }
        }

        private UIModifier BuildPageModifier() =>
                UIModifier.New
                        .FillMaxWidth()
                        .Padding(verticalPx: PAGE_VERTICAL_PADDING_PX, horizontalPx: ZERO_PX);

        private UIModifier BuildTestPanelModifier() =>
                UIModifier.New
                        .FillMaxWidth()
                        .Padding(uniformPx: PANEL_PADDING_PX)
                        .BorderRadius(px: PANEL_BORDER_RADIUS_PX)
                        .Border(thicknessPx: PANEL_BORDER_THICKNESS_PX, borderColor: PANEL_BORDER_COLOR_VAR);

        private UIModifier BuildPreviewBoxModifier() =>
                UIModifier.New
                        .MaxWidth(px: PREVIEW_BOX_MAX_WIDTH_PX)
                        .MaxHeight(px: PREVIEW_BOX_MAX_HEIGHT_PX)
                        .ClipRounded(px: PREVIEW_BOX_RADIUS_PX);

        private UIModifier BuildPreviewImageModifier() =>
                UIModifier.New
                        .MaxWidth(px: PREVIEW_BOX_MAX_WIDTH_PX)
                        .MaxHeight(px: PREVIEW_BOX_MAX_HEIGHT_PX);

        private UIModifier BuildMetadataColumnModifier() =>
                UIModifier.New
                        .FlexGrow(growFactor: METADATA_COL_FLEX_GROW)
                        .MinWidth(px: METADATA_COL_MIN_WIDTH_PX);

        private string ResolveSelectedImageSource()
        {
                if (this._selectedImage is null)
                {
                        return string.Empty;
                }

                try
                {
                        return this.ImageClient.GetImageContentUri(imageId: this._selectedImage.Id).AbsoluteUri;
                }
                catch (Exception ex) when (ex is InvalidOperationException or OptionsValidationException)
                {
                        return string.Empty;
                }
        }

        /// <summary>
        /// Counts the total bytes in <paramref name="stream"/> by reading sequentially through a small fixed buffer instead of materializing the full image in memory.
        /// </summary>
        /// <param name="stream">The response stream to drain and measure.</param>
        /// <returns>The total number of bytes read from <paramref name="stream"/>.</returns>
        private static async Task<long> CountStreamBytesAsync(Stream stream)
        {
                // Stream through a fixed 80 KiB LOH-free buffer instead of buffering the entire image in a MemoryStream.
                byte[] buffer = new byte[STREAM_COPY_BUFFER_BYTES];
                long totalBytes = 0;
                int bytesRead;

                // Pass Memory<byte> so ReadAsync writes directly into the reusable buffer without extra array copies.
                while ((bytesRead = await stream.ReadAsync(buffer: buffer.AsMemory())) > 0)
                {
                        totalBytes += bytesRead;
                }

                return totalBytes;
        }
}
