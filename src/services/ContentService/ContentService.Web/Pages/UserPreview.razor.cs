using System.Text.Json;
using ALKsPersonalUI.Core;
using ALKsPersonalUI.Indicators;
using ALKsPersonalUI.Overlays;
using ContentService.Sdk.Clients;
using ContentService.Sdk.Exceptions;
using ContentService.Sdk.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;

namespace ContentService.Web.Pages;

/// <summary>
/// Production-ready user preview of ContentService powered by <c>ContentService.Sdk</c> and <c>ContentService.Components</c>.
/// </summary>
public sealed partial class UserPreview : ComponentBase, IDisposable
{
        private const int STREAM_COPY_BUFFER_BYTES = 81920;
        private const long BYTES_PER_KILOBYTE = 1024;
        private const long BYTES_PER_MEGABYTE = BYTES_PER_KILOBYTE * BYTES_PER_KILOBYTE;
        private const double PANEL_PADDING_PX = 16;
        private const double PANEL_BORDER_RADIUS_PX = 12;
        private const double PANEL_BORDER_THICKNESS_PX = 1;
        private const double PREVIEW_BOX_MAX_WIDTH_PX = 360;
        private const double PREVIEW_BOX_MAX_HEIGHT_PX = 260;
        private const double PREVIEW_BOX_RADIUS_PX = 8;
        private const double METADATA_COL_MIN_WIDTH_PX = 260;
        private const double METADATA_COL_FLEX_GROW = 1;
        private const string PANEL_BORDER_COLOR_VAR = "var(--pui-border)";

        private readonly CancellationTokenSource _disposeCts = new();
        private PreviewTab _activeTab = PreviewTab.MediaLibrary;
        private ContentStorageHealthReport? _storageHealth;
        private ContentImageMetadata? _selectedImage;
        private ContentTemplateMetadata? _selectedTemplate;
        private ContentPublishBundle? _resolvedBundle;
        private bool _isLoadingHealth;
        private bool _isDownloadingImage;
        private bool _isResolvingBundle;
        private bool _isDisposed;

        [Inject]
        private IContentServiceImageClient ImageClient { get; set; } = default!;

        [Inject]
        private IContentServicePublishingClient PublishingClient { get; set; } = default!;

        [Inject]
        private IContentServiceHealthClient HealthClient { get; set; } = default!;

        [Inject]
        private ToastService Toasts { get; set; } = default!;

        /// <inheritdoc />
        protected override async Task OnInitializedAsync()
        {
                await this.RefreshStorageHealthAsync();
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

        private void SelectTab(PreviewTab tab) => this._activeTab = tab;

        private async Task RefreshStorageHealthAsync()
        {
                if (this._isLoadingHealth || this._isDisposed)
                {
                        return;
                }

                this._isLoadingHealth = true;
                try
                {
                        this._storageHealth = await this.HealthClient.GetStorageHealthAsync(cancellationToken: this._disposeCts.Token);
                }
                catch (OperationCanceledException)
                {
                        return;
                }
                catch (Exception ex) when (ex is ContentServiceApiException or HttpRequestException or InvalidOperationException or OptionsValidationException)
                {
                        this._storageHealth = null;
                }
                finally
                {
                        this._isLoadingHealth = false;
                }
        }

        private Task HandleImageSelectedAsync(ContentImageMetadata selectedImage)
        {
                this._selectedImage = selectedImage;
                this._resolvedBundle = null;
                this.Toasts.ShowInfo(messageText: $"Selected '{selectedImage.FileName}'.");
                return Task.CompletedTask;
        }

        private Task HandleTemplateSelectedAsync(ContentTemplateMetadata selectedTemplate)
        {
                this._selectedTemplate = selectedTemplate;
                this._resolvedBundle = null;
                this.Toasts.ShowInfo(messageText: $"Selected template '{selectedTemplate.Name}'.");
                return Task.CompletedTask;
        }

        private async Task HandleDownloadSelectedImageAsync()
        {
                if (this._selectedImage is null || this._isDownloadingImage)
                {
                        return;
                }

                this._isDownloadingImage = true;
                try
                {
                        // Dispose ContentImageDownload immediately after streaming so the HTTP connection is released cleanly.
                        using ContentImageDownload download = await this.ImageClient.DownloadImageAsync(
                                imageId: this._selectedImage.Id,
                                cancellationToken: this._disposeCts.Token);
                        long downloadedBytes = await CountStreamBytesAsync(
                                stream: download.Content,
                                cancellationToken: this._disposeCts.Token);

                        this.Toasts.ShowSuccess(messageText: $"Verified '{this._selectedImage.FileName}' ({FormatByteSize(bytes: downloadedBytes)}).");
                }
                catch (OperationCanceledException)
                {
                        return;
                }
                catch (ContentServiceApiException ex)
                {
                        this.Toasts.ShowDanger(messageText: ex.ProblemDetails?.Detail ?? ex.Message);
                }
                catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or OptionsValidationException)
                {
                        this.Toasts.ShowDanger(messageText: ex.Message);
                }
                finally
                {
                        this._isDownloadingImage = false;
                }
        }

        private async Task HandleResolvePublishBundleAsync()
        {
                if (this._isResolvingBundle)
                {
                        return;
                }

                this._isResolvingBundle = true;
                try
                {
                        // Include the currently selected image reference when present so users can verify a real asset bundle.
                        string implementationJson = this._selectedImage is null
                                ? "{}"
                                : JsonSerializer.Serialize(value: new { picture = this._selectedImage.PictureReference });

                        ResolvePublishBundleRequest request = new()
                        {
                                TemplateId = this._selectedTemplate?.Id,
                                ImplementationJson = implementationJson
                        };

                        this._resolvedBundle = await this.PublishingClient.ResolvePublishBundleAsync(
                                request: request,
                                cancellationToken: this._disposeCts.Token);

                        this.Toasts.ShowSuccess(messageText: $"Resolved bundle with {this._resolvedBundle.Assets.Count} verified asset(s).");
                }
                catch (OperationCanceledException)
                {
                        return;
                }
                catch (ContentServiceApiException ex)
                {
                        this.Toasts.ShowDanger(messageText: ex.ProblemDetails?.Detail ?? ex.Message);
                }
                catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or OptionsValidationException)
                {
                        this.Toasts.ShowDanger(messageText: ex.Message);
                }
                finally
                {
                        this._isResolvingBundle = false;
                }
        }

        private string BuildStorageStatusLabel()
        {
                if (this._storageHealth is null)
                {
                        return "Storage: Offline";
                }

                return $"Storage: {this._storageHealth.Status} ({this._storageHealth.UtilizationPercent:0.#}%)";
        }

        private Pill.PillVariant ResolveStorageStatusVariant() =>
                this._storageHealth?.Status switch
                {
                        ContentStorageHealthStatus.Healthy => Pill.PillVariant.Success,
                        ContentStorageHealthStatus.Warning => Pill.PillVariant.Warning,
                        ContentStorageHealthStatus.FullReadOnly => Pill.PillVariant.Danger,
                        _ => Pill.PillVariant.Neutral
                };

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

        private UIModifier BuildPanelModifier() =>
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

        private static string FormatByteSize(long bytes)
        {
                if (bytes >= BYTES_PER_MEGABYTE)
                {
                        return $"{bytes / (double)BYTES_PER_MEGABYTE:0.##} MB";
                }

                if (bytes >= BYTES_PER_KILOBYTE)
                {
                        return $"{bytes / (double)BYTES_PER_KILOBYTE:0.#} KB";
                }

                return $"{bytes:N0} B";
        }

        private static async Task<long> CountStreamBytesAsync(
                Stream stream,
                CancellationToken cancellationToken)
        {
                // Stream through a fixed 80 KiB buffer to avoid buffering the entire asset in memory.
                byte[] buffer = new byte[STREAM_COPY_BUFFER_BYTES];
                long totalBytes = 0;
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer: buffer.AsMemory(), cancellationToken: cancellationToken)) > 0)
                {
                        totalBytes += bytesRead;
                }

                return totalBytes;
        }

        private enum PreviewTab
        {
                MediaLibrary = 0,
                Templates = 1,
                PublishPreview = 2
        }
}

